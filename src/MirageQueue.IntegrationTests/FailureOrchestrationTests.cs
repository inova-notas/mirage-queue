using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.Consumers;
using MirageQueue.IntegrationTests.Fixtures;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using MirageQueue.Retry;
using MirageQueue.Workers;
using Xunit;

namespace MirageQueue.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class FailureOrchestrationTests
{
    private readonly PostgresFixture _fixture;

    public FailureOrchestrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HandleFailureAsync_NoExplicitPolicy_TransitionsToFailed()
    {
        // Default policy: MaxAttempts=1. AttemptCount=0 → newAttempts=1 → not < 1 → terminal.
        // No explicit policy → Failed (not DeadLettered).
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, "unregistered-endpoint", OutboundMessageStatus.Processing);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        var row = await LoadRowAsync(outboundId);
        await ProcessOutboundMessagesWorker.HandleFailureAsync(row, new TimeoutException("boom"), repo);

        var after = await LoadRowAsync(outboundId);
        Assert.Equal(OutboundMessageStatus.Failed, after.Status);
        Assert.Null(after.ProcessingStartedAt);
        Assert.Equal("boom", after.ErrorMessage);
    }

    [Fact]
    public async Task HandleFailureAsync_ExplicitPolicyWithRoomToRetry_TransitionsToNewWithBackoff()
    {
        await _fixture.ResetAsync();

        var endpoint = RegisterPolicyConsumer(maxAttempts: 3, backoffSeconds: 5);

        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, endpoint, OutboundMessageStatus.Processing, attemptCount: 0);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        var row = await LoadRowAsync(outboundId);
        await ProcessOutboundMessagesWorker.HandleFailureAsync(row, new TimeoutException("boom"), repo);

        var after = await LoadRowAsync(outboundId);
        Assert.Equal(OutboundMessageStatus.New, after.Status);
        Assert.Equal(1, after.AttemptCount);
        Assert.NotNull(after.NextRetryAt);
        Assert.True(after.NextRetryAt > DateTime.UtcNow);
        Assert.Null(after.ProcessingStartedAt);
    }

    [Fact]
    public async Task HandleFailureAsync_ExplicitPolicyExhausted_TransitionsToDeadLettered()
    {
        await _fixture.ResetAsync();

        var endpoint = RegisterPolicyConsumer(maxAttempts: 3, backoffSeconds: 5);

        var inboundId = await _fixture.SeedInboundAsync();
        // AttemptCount=2 → newAttempts=3 → not < 3 → terminal → DeadLettered (policy attached).
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, endpoint, OutboundMessageStatus.Processing, attemptCount: 2);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        var row = await LoadRowAsync(outboundId);
        await ProcessOutboundMessagesWorker.HandleFailureAsync(row, new TimeoutException("exhausted"), repo);

        var after = await LoadRowAsync(outboundId);
        Assert.Equal(OutboundMessageStatus.DeadLettered, after.Status);
        Assert.Null(after.ProcessingStartedAt);
        Assert.Equal("exhausted", after.ErrorMessage);
    }

    [Fact]
    public async Task HandleFailureAsync_WrappedException_CapturesInnerExceptionDetails()
    {
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, "unregistered-endpoint", OutboundMessageStatus.Processing);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        var inner = new TimeoutException("inner cause");
        var wrapped = new InvalidOperationException("outer wrapper", inner);

        var row = await LoadRowAsync(outboundId);
        await ProcessOutboundMessagesWorker.HandleFailureAsync(row, wrapped, repo);

        var after = await LoadRowAsync(outboundId);
        Assert.Equal("inner cause", after.ErrorMessage);
        Assert.Equal(typeof(TimeoutException).FullName, after.ExceptionType);
    }

    private string RegisterPolicyConsumer(int maxAttempts, int backoffSeconds)
    {
        var endpoint = $"policy-consumer-{Guid.NewGuid():N}";
        var policy = new RetryPolicy
        {
            MaxAttempts = maxAttempts,
            TransientAttempts = 0,
            Backoff = Backoff.Constant(TimeSpan.FromSeconds(backoffSeconds)),
        };
        RegisterConsumerStub(endpoint, policy, hasExplicitPolicy: true);
        return endpoint;
    }

    private static void RegisterConsumerStub(string endpoint, RetryPolicy policy, bool hasExplicitPolicy)
    {
        var consumersByEndpointField = typeof(DispatcherContext).GetField("ConsumersByEndpoint",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var registeredConsumersField = typeof(DispatcherContext).GetField("RegisteredConsumers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var consumer = new DispatcherConsumer
        {
            MessageContract = "Stub.Contract",
            ConsumerEndpoint = endpoint,
            ConsumerType = typeof(StubType),
            MessageType = typeof(object),
            RetryPolicy = policy,
            HasExplicitPolicy = hasExplicitPolicy,
        };

        var byEndpoint = consumersByEndpointField!.GetValue(null)!;
        var registered = (List<DispatcherConsumer>)registeredConsumersField!.GetValue(null)!;
        byEndpoint.GetType().GetMethod("TryAdd")!.Invoke(byEndpoint, new object[] { endpoint, consumer });
        registered.Add(consumer);
    }

    private async Task<OutboundMessage> LoadRowAsync(Guid id)
    {
        await using var verify = _fixture.CreateMirageQueueDbContext();
        return await verify.Set<OutboundMessage>().AsNoTracking().FirstAsync(x => x.Id == id);
    }

    private sealed class StubType { }
}

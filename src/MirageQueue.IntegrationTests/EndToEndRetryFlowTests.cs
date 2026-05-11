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
public class EndToEndRetryFlowTests
{
    private readonly PostgresFixture _fixture;

    public EndToEndRetryFlowTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FailingConsumer_ProgressesThroughRetriesUntilDeadLettered()
    {
        // Simulates the worker loop: pickup → dispatch fails → HandleFailureAsync → backoff → next pickup.
        // After MaxAttempts failures, the row terminates as DeadLettered.
        await _fixture.ResetAsync();

        var endpoint = RegisterPolicyConsumer(maxAttempts: 3);
        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, endpoint, OutboundMessageStatus.New, attemptCount: 0);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        // --- Attempt 1: pickup → fail → MarkForRetry ---
        await SimulatePickupAndFailureAsync(outboundId, repo, "transient #1");

        var afterFirst = await LoadRowAsync(outboundId);
        Assert.Equal(OutboundMessageStatus.New, afterFirst.Status);
        Assert.Equal(1, afterFirst.AttemptCount);
        Assert.NotNull(afterFirst.NextRetryAt);

        // Reset NextRetryAt to past so the next pickup sees it.
        await ClearNextRetryAtAsync(outboundId);

        // --- Attempt 2: pickup → fail → MarkForRetry ---
        await SimulatePickupAndFailureAsync(outboundId, repo, "transient #2");

        var afterSecond = await LoadRowAsync(outboundId);
        Assert.Equal(OutboundMessageStatus.New, afterSecond.Status);
        Assert.Equal(2, afterSecond.AttemptCount);

        await ClearNextRetryAtAsync(outboundId);

        // --- Attempt 3: pickup → fail → DeadLettered (newAttempts=3, MaxAttempts=3) ---
        await SimulatePickupAndFailureAsync(outboundId, repo, "transient #3 exhausts");

        var final = await LoadRowAsync(outboundId);
        Assert.Equal(OutboundMessageStatus.DeadLettered, final.Status);
        Assert.Equal("transient #3 exhausts", final.ErrorMessage);
        Assert.Null(final.ProcessingStartedAt);
    }

    [Fact]
    public async Task RowWithFutureNextRetryAt_NotPickedUpUntilCleared()
    {
        // Pickup query honors NextRetryAt: a row with a future NextRetryAt
        // is invisible until that time passes.
        await _fixture.ResetAsync();

        var endpoint = RegisterPolicyConsumer(maxAttempts: 5);
        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, endpoint, OutboundMessageStatus.New,
            nextRetryAt: DateTime.UtcNow.AddHours(1),
            attemptCount: 1);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        var picked = await repo.GetQueuedMessages(limit: 10);
        Assert.DoesNotContain(picked, m => m.Id == outboundId);

        // Move NextRetryAt to the past; now it should be eligible.
        await ClearNextRetryAtAsync(outboundId);
        var pickedNow = await repo.GetQueuedMessages(limit: 10);
        Assert.Contains(pickedNow, m => m.Id == outboundId);
    }

    private async Task SimulatePickupAndFailureAsync(Guid id, IOutboundMessageRepository repo, string errorMessage)
    {
        // Mimic the worker: transactional pickup → MarkProcessing → dispatch fails → HandleFailureAsync.
        await repo.MarkProcessing(id);
        var row = await LoadRowAsync(id);
        await ProcessOutboundMessagesWorker.HandleFailureAsync(row, new TimeoutException(errorMessage), repo);
    }

    private string RegisterPolicyConsumer(int maxAttempts)
    {
        var endpoint = $"e2e-policy-{Guid.NewGuid():N}";
        var policy = new RetryPolicy
        {
            MaxAttempts = maxAttempts,
            TransientAttempts = 0,
            Backoff = Backoff.Constant(TimeSpan.FromSeconds(1)),
        };

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
            HasExplicitPolicy = true,
        };

        var byEndpoint = consumersByEndpointField!.GetValue(null)!;
        var registered = (List<DispatcherConsumer>)registeredConsumersField!.GetValue(null)!;
        byEndpoint.GetType().GetMethod("TryAdd")!.Invoke(byEndpoint, new object[] { endpoint, consumer });
        registered.Add(consumer);

        return endpoint;
    }

    private async Task ClearNextRetryAtAsync(Guid id)
    {
        // Move NextRetryAt to the past via direct SQL so the test isn't time-bound.
        await using var conn = new Npgsql.NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """UPDATE mirage_queue."OutboundMessage" SET "NextRetryAt" = now() - interval '1 minute' WHERE "Id" = @id""";
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("id", id));
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<OutboundMessage> LoadRowAsync(Guid id)
    {
        await using var verify = _fixture.CreateMirageQueueDbContext();
        return await verify.Set<OutboundMessage>().AsNoTracking().FirstAsync(x => x.Id == id);
    }

    private sealed class StubType { }
}

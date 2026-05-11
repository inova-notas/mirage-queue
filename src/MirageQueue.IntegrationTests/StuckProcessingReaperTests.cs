using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MirageQueue.Consumers;
using MirageQueue.IntegrationTests.Fixtures;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using MirageQueue.Postgres.Workers;
using MirageQueue.Retry;
using MirageQueue.Workers;
using Npgsql;
using Xunit;

namespace MirageQueue.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class StuckProcessingReaperTests
{
    private readonly PostgresFixture _fixture;

    public StuckProcessingReaperTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetStuckProcessingMessages_ReturnsOnlyAgedProcessingRows()
    {
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();

        // Aged Processing row (eligible)
        var oldId = await _fixture.SeedOutboundAsync(inboundId, "endpoint-old", OutboundMessageStatus.Processing,
            processingStartedAt: DateTime.UtcNow.AddMinutes(-10));

        // Fresh Processing row (not yet stuck)
        await _fixture.SeedOutboundAsync(inboundId, "endpoint-fresh", OutboundMessageStatus.Processing,
            processingStartedAt: DateTime.UtcNow.AddSeconds(-5));

        // Non-Processing row (ineligible regardless of age)
        await _fixture.SeedOutboundAsync(inboundId, "endpoint-new", OutboundMessageStatus.New,
            processingStartedAt: DateTime.UtcNow.AddMinutes(-10));

        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        var stuck = await repo.GetStuckProcessingMessages(TimeSpan.FromMinutes(5), limit: 10);

        Assert.Single(stuck);
        Assert.Equal(oldId, stuck[0].Id);
    }

    [Fact]
    public async Task Reaper_StuckRowWithoutExplicitPolicy_TransitionsToFailed()
    {
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        var stuckId = await _fixture.SeedOutboundAsync(inboundId, "endpoint-reap-no-policy", OutboundMessageStatus.Processing,
            processingStartedAt: DateTime.UtcNow.AddMinutes(-10),
            attemptCount: 0);

        var reaper = BuildReaperWithShortLease();
        await reaper.ReapOnceAsync(CancellationToken.None);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var row = await verify.Set<OutboundMessage>().AsNoTracking().FirstAsync(x => x.Id == stuckId);

        // No explicit policy → default MaxAttempts=1 → newAttempts=1 is NOT < 1 → terminate.
        // No policy attached → terminate as Failed (preserves pre-Phase-3 behavior).
        Assert.Equal(OutboundMessageStatus.Failed, row.Status);
        Assert.Null(row.ProcessingStartedAt);
        Assert.Contains("Processing lease expired", row.ErrorMessage);
    }

    [Fact]
    public async Task Reaper_StuckRowWithExplicitPolicy_RoomToRetry_ResetsToNewWithBackoff()
    {
        await _fixture.ResetAsync();

        // Register a consumer with an explicit retry policy.
        var endpoint = $"endpoint-reap-policy-{Guid.NewGuid():N}";
        var policy = new RetryPolicy
        {
            MaxAttempts = 3,
            TransientAttempts = 0,
            Backoff = Backoff.Constant(TimeSpan.FromSeconds(30)),
        };
        RegisterConsumerStub(endpoint, policy, hasExplicitPolicy: true);

        var inboundId = await _fixture.SeedInboundAsync();
        var stuckId = await _fixture.SeedOutboundAsync(inboundId, endpoint, OutboundMessageStatus.Processing,
            processingStartedAt: DateTime.UtcNow.AddMinutes(-10),
            attemptCount: 0);

        var reaper = BuildReaperWithShortLease();
        await reaper.ReapOnceAsync(CancellationToken.None);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var row = await verify.Set<OutboundMessage>().AsNoTracking().FirstAsync(x => x.Id == stuckId);

        Assert.Equal(OutboundMessageStatus.New, row.Status);
        Assert.Equal(1, row.AttemptCount);
        Assert.NotNull(row.NextRetryAt);
        Assert.True(row.NextRetryAt > DateTime.UtcNow); // future
        Assert.Null(row.ProcessingStartedAt);
        Assert.Contains("Processing lease expired", row.ErrorMessage);
    }

    [Fact]
    public async Task Reaper_StuckRowWithExplicitPolicy_AttemptsExhausted_DeadLetters()
    {
        await _fixture.ResetAsync();

        var endpoint = $"endpoint-reap-exhausted-{Guid.NewGuid():N}";
        var policy = new RetryPolicy { MaxAttempts = 3, TransientAttempts = 0, Backoff = Backoff.None };
        RegisterConsumerStub(endpoint, policy, hasExplicitPolicy: true);

        var inboundId = await _fixture.SeedInboundAsync();
        // Already at MaxAttempts-1: one more failure exhausts.
        var stuckId = await _fixture.SeedOutboundAsync(inboundId, endpoint, OutboundMessageStatus.Processing,
            processingStartedAt: DateTime.UtcNow.AddMinutes(-10),
            attemptCount: 2);

        var reaper = BuildReaperWithShortLease();
        await reaper.ReapOnceAsync(CancellationToken.None);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var row = await verify.Set<OutboundMessage>().AsNoTracking().FirstAsync(x => x.Id == stuckId);

        Assert.Equal(OutboundMessageStatus.DeadLettered, row.Status);
        Assert.Null(row.ProcessingStartedAt);
        Assert.Contains("Processing lease expired", row.ErrorMessage);
    }

    private static void RegisterConsumerStub(string endpoint, RetryPolicy policy, bool hasExplicitPolicy)
    {
        // Reflection-free registration into the static DispatcherContext for test isolation.
        // The static dictionary is keyed by ConsumerEndpoint (we use a unique endpoint per test
        // to avoid cross-test contamination of the shared registry).
        var fakeType = typeof(StubConsumerType);
        var consumersByEndpointField = typeof(DispatcherContext).GetField("ConsumersByEndpoint",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var registeredConsumersField = typeof(DispatcherContext).GetField("RegisteredConsumers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var consumer = new DispatcherConsumer
        {
            MessageContract = "Stub.Contract",
            ConsumerEndpoint = endpoint,
            ConsumerType = fakeType,
            MessageType = typeof(object),
            RetryPolicy = policy,
            HasExplicitPolicy = hasExplicitPolicy,
        };

        var byEndpoint = consumersByEndpointField!.GetValue(null);
        var registered = (List<DispatcherConsumer>)registeredConsumersField!.GetValue(null)!;

        // ConcurrentDictionary doesn't expose Add directly via base interface; use TryAdd via reflection.
        var tryAdd = byEndpoint!.GetType().GetMethod("TryAdd")!;
        tryAdd.Invoke(byEndpoint, new object[] { endpoint, consumer });
        registered.Add(consumer);
    }

    private sealed class StubConsumerType { }

    [Fact]
    public async Task Reaper_DoesNotTouchFreshProcessingRows()
    {
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        var freshId = await _fixture.SeedOutboundAsync(inboundId, "endpoint-fresh-2", OutboundMessageStatus.Processing,
            processingStartedAt: DateTime.UtcNow.AddSeconds(-5));

        var reaper = BuildReaperWithShortLease();
        await reaper.ReapOnceAsync(CancellationToken.None);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var row = await verify.Set<OutboundMessage>().AsNoTracking().FirstAsync(x => x.Id == freshId);

        Assert.Equal(OutboundMessageStatus.Processing, row.Status);
        Assert.NotNull(row.ProcessingStartedAt);
    }

    private PgStuckProcessingReaperWorker BuildReaperWithShortLease()
    {
        var config = new MirageQueueConfiguration
        {
            ProcessingLeaseDuration = TimeSpan.FromMinutes(1),
            StuckProcessingPollingTime = 60000,
        };
        return new PgStuckProcessingReaperWorker(
            _fixture.Services,
            NullLogger<StuckProcessingReaperWorker>.Instance,
            config);
    }

    [Fact]
    public async Task Reaper_MultipleStuckRowsInOneSweep_AllProcessedAccordingToTheirPolicy()
    {
        await _fixture.ResetAsync();

        var roomyEndpoint = $"reap-multi-roomy-{Guid.NewGuid():N}";
        var exhaustedEndpoint = $"reap-multi-exhausted-{Guid.NewGuid():N}";

        RegisterConsumerStub(roomyEndpoint,
            new RetryPolicy { MaxAttempts = 5, TransientAttempts = 0, Backoff = Backoff.Constant(TimeSpan.FromSeconds(30)) },
            hasExplicitPolicy: true);
        RegisterConsumerStub(exhaustedEndpoint,
            new RetryPolicy { MaxAttempts = 3, TransientAttempts = 0, Backoff = Backoff.None },
            hasExplicitPolicy: true);

        var inboundId = await _fixture.SeedInboundAsync();

        // Three stuck rows targeting three different policy regimes:
        //   - roomy: AttemptCount=1, MaxAttempts=5 → MarkForRetry
        //   - exhausted: AttemptCount=2, MaxAttempts=3 → DeadLettered
        //   - no policy: → Failed
        var roomyId = await _fixture.SeedOutboundAsync(inboundId, roomyEndpoint, OutboundMessageStatus.Processing,
            processingStartedAt: DateTime.UtcNow.AddMinutes(-10), attemptCount: 1);
        var exhaustedId = await _fixture.SeedOutboundAsync(inboundId, exhaustedEndpoint, OutboundMessageStatus.Processing,
            processingStartedAt: DateTime.UtcNow.AddMinutes(-10), attemptCount: 2);
        var noPolicyId = await _fixture.SeedOutboundAsync(inboundId, "unregistered-endpoint", OutboundMessageStatus.Processing,
            processingStartedAt: DateTime.UtcNow.AddMinutes(-10));

        var reaper = BuildReaperWithShortLease();
        await reaper.ReapOnceAsync(CancellationToken.None);

        await using var verify = _fixture.CreateMirageQueueDbContext();

        var roomy = await verify.Set<OutboundMessage>().AsNoTracking().FirstAsync(x => x.Id == roomyId);
        Assert.Equal(OutboundMessageStatus.New, roomy.Status);
        Assert.Equal(2, roomy.AttemptCount);
        Assert.NotNull(roomy.NextRetryAt);

        var exhausted = await verify.Set<OutboundMessage>().AsNoTracking().FirstAsync(x => x.Id == exhaustedId);
        Assert.Equal(OutboundMessageStatus.DeadLettered, exhausted.Status);

        var noPolicy = await verify.Set<OutboundMessage>().AsNoTracking().FirstAsync(x => x.Id == noPolicyId);
        Assert.Equal(OutboundMessageStatus.Failed, noPolicy.Status);
    }
}

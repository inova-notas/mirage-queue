using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.Dashboard.Services;
using MirageQueue.IntegrationTests.Fixtures;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using Xunit;

namespace MirageQueue.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class DashboardServiceTests
{
    private readonly PostgresFixture _fixture;

    public DashboardServiceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ReplayDeadLetterAsync_ResetsDeadLetteredRow()
    {
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, "ep-dlq-1", OutboundMessageStatus.DeadLettered,
            attemptCount: 5,
            nextRetryAt: DateTime.UtcNow.AddMinutes(10));

        await using var scope = _fixture.Services.CreateAsyncScope();
        var service = BuildDashboardService(scope);

        var success = await service.ReplayDeadLetterAsync(outboundId);

        Assert.True(success);
        var row = await LoadRowAsync(outboundId);
        Assert.Equal(OutboundMessageStatus.New, row.Status);
        Assert.Equal(0, row.AttemptCount);
        Assert.Null(row.NextRetryAt);
    }

    [Fact]
    public async Task ReplayDeadLetterAsync_NonDeadLetteredRow_NoOpButReturnsTrue()
    {
        // The repo's ReplayFromDeadLetter is a guarded UPDATE (only acts on DeadLettered rows).
        // It doesn't throw; the dashboard wrapper returns true since no exception.
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, "ep-processed", OutboundMessageStatus.Processed, attemptCount: 2);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var service = BuildDashboardService(scope);

        var success = await service.ReplayDeadLetterAsync(outboundId);

        Assert.True(success);
        var row = await LoadRowAsync(outboundId);
        Assert.Equal(OutboundMessageStatus.Processed, row.Status); // unchanged
        Assert.Equal(2, row.AttemptCount);
    }

    [Fact]
    public async Task DeleteOutboundMessageAsync_RemovesRow()
    {
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, "ep-delete", OutboundMessageStatus.DeadLettered);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var service = BuildDashboardService(scope);

        var success = await service.DeleteOutboundMessageAsync(outboundId);

        Assert.True(success);
        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(0, await verify.Set<OutboundMessage>().CountAsync(x => x.Id == outboundId));
    }

    [Fact]
    public async Task DeleteOutboundMessageAsync_NonExistentRow_ReturnsFalse()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var service = BuildDashboardService(scope);

        var success = await service.DeleteOutboundMessageAsync(Guid.NewGuid());

        Assert.False(success);
    }

    [Fact]
    public async Task RequeueOutboundMessageAsync_DeadLetteredRow_RoutesThroughReplay()
    {
        // RequeueOutboundMessageAsync was updated to detect DeadLettered status and
        // call ReplayFromDeadLetter (which resets AttemptCount and NextRetryAt).
        // The pre-Phase-3 path (UpdateMessageStatus only) would leave AttemptCount
        // at its old value, causing immediate re-deadletter on the next dispatch.
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, "ep-requeue-dlq", OutboundMessageStatus.DeadLettered,
            attemptCount: 4,
            nextRetryAt: DateTime.UtcNow.AddMinutes(30));

        await using var scope = _fixture.Services.CreateAsyncScope();
        var service = BuildDashboardService(scope);

        var success = await service.RequeueOutboundMessageAsync(outboundId);

        Assert.True(success);
        var row = await LoadRowAsync(outboundId);
        Assert.Equal(OutboundMessageStatus.New, row.Status);
        Assert.Equal(0, row.AttemptCount);     // reset
        Assert.Null(row.NextRetryAt);          // reset
    }

    [Fact]
    public async Task RequeueOutboundMessageAsync_NonDeadLetteredRow_UsesPlainStatusUpdate()
    {
        // For a row that's NOT DeadLettered (e.g., Failed), the standard status update
        // path runs — AttemptCount is preserved.
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, "ep-requeue-failed", OutboundMessageStatus.Failed,
            attemptCount: 2);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var service = BuildDashboardService(scope);

        var success = await service.RequeueOutboundMessageAsync(outboundId);

        Assert.True(success);
        var row = await LoadRowAsync(outboundId);
        Assert.Equal(OutboundMessageStatus.New, row.Status);
        Assert.Equal(2, row.AttemptCount); // preserved (plain UpdateMessageStatus path)
    }

    private static DashboardService BuildDashboardService(AsyncServiceScope scope)
    {
        var inboundRepo = scope.ServiceProvider.GetRequiredService<IInboundMessageRepository>();
        var outboundRepo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();
        var scheduledRepo = scope.ServiceProvider.GetRequiredService<IScheduledMessageRepository>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new DashboardService(inboundRepo, outboundRepo, scheduledRepo, cache);
    }

    private async Task<OutboundMessage> LoadRowAsync(Guid id)
    {
        await using var verify = _fixture.CreateMirageQueueDbContext();
        return await verify.Set<OutboundMessage>().AsNoTracking().FirstAsync(x => x.Id == id);
    }
}

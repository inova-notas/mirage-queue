using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MirageQueue.IntegrationTests.Fixtures;
using MirageQueue.Messages.Entities;
using MirageQueue.Postgres.Workers;
using MirageQueue.Workers;
using Xunit;

namespace MirageQueue.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class RetentionCleanupTests
{
    private readonly PostgresFixture _fixture;

    public RetentionCleanupTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CleanupOnce_DeletesOldProcessedOutbound()
    {
        await _fixture.ResetAsync();
        var inboundId = await _fixture.SeedInboundAsync(updateAt: DateTime.UtcNow.AddDays(-100));

        var oldId = await _fixture.SeedOutboundAsync(inboundId, $"e-{Guid.NewGuid():N}",
            OutboundMessageStatus.Processed, updateAt: DateTime.UtcNow.AddDays(-100));

        var worker = BuildCleanupWorker(retentionDays: 90);
        await worker.CleanupOnceAsync(CancellationToken.None);

        Assert.Equal(0, await CountOutboundAsync(oldId));
    }

    [Fact]
    public async Task CleanupOnce_DeletesOldDeadLetteredOutbound()
    {
        await _fixture.ResetAsync();
        var inboundId = await _fixture.SeedInboundAsync(updateAt: DateTime.UtcNow.AddDays(-100));

        var oldId = await _fixture.SeedOutboundAsync(inboundId, $"e-{Guid.NewGuid():N}",
            OutboundMessageStatus.DeadLettered, updateAt: DateTime.UtcNow.AddDays(-100));

        var worker = BuildCleanupWorker(retentionDays: 90);
        await worker.CleanupOnceAsync(CancellationToken.None);

        Assert.Equal(0, await CountOutboundAsync(oldId));
    }

    [Fact]
    public async Task CleanupOnce_DoesNotDeleteOldFailedOutbound()
    {
        // Failed is intentionally excluded from cleanup — operators without a retry policy
        // rely on Failed for forensic context.
        await _fixture.ResetAsync();
        var inboundId = await _fixture.SeedInboundAsync(updateAt: DateTime.UtcNow.AddDays(-100));

        var failedId = await _fixture.SeedOutboundAsync(inboundId, $"e-{Guid.NewGuid():N}",
            OutboundMessageStatus.Failed, updateAt: DateTime.UtcNow.AddDays(-100));

        var worker = BuildCleanupWorker(retentionDays: 90);
        await worker.CleanupOnceAsync(CancellationToken.None);

        Assert.Equal(1, await CountOutboundAsync(failedId));
    }

    [Fact]
    public async Task CleanupOnce_DoesNotDeleteOldNewOrProcessingOutbound()
    {
        await _fixture.ResetAsync();
        var inboundId = await _fixture.SeedInboundAsync(updateAt: DateTime.UtcNow.AddDays(-100));

        var newId = await _fixture.SeedOutboundAsync(inboundId, $"e-{Guid.NewGuid():N}",
            OutboundMessageStatus.New, updateAt: DateTime.UtcNow.AddDays(-100));
        var procId = await _fixture.SeedOutboundAsync(inboundId, $"e-{Guid.NewGuid():N}",
            OutboundMessageStatus.Processing, updateAt: DateTime.UtcNow.AddDays(-100));

        var worker = BuildCleanupWorker(retentionDays: 90);
        await worker.CleanupOnceAsync(CancellationToken.None);

        Assert.Equal(1, await CountOutboundAsync(newId));
        Assert.Equal(1, await CountOutboundAsync(procId));
    }

    [Fact]
    public async Task CleanupOnce_DoesNotDeleteRecentTerminalOutbound()
    {
        await _fixture.ResetAsync();
        var inboundId = await _fixture.SeedInboundAsync(updateAt: DateTime.UtcNow.AddDays(-1));

        var freshId = await _fixture.SeedOutboundAsync(inboundId, $"e-{Guid.NewGuid():N}",
            OutboundMessageStatus.Processed, updateAt: DateTime.UtcNow.AddDays(-1));

        var worker = BuildCleanupWorker(retentionDays: 90);
        await worker.CleanupOnceAsync(CancellationToken.None);

        Assert.Equal(1, await CountOutboundAsync(freshId));
    }

    [Fact]
    public async Task CleanupOnce_DeletesOldQueuedInbound_WhenAllChildrenAreTerminal()
    {
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync(InboundMessageStatus.Queued,
            updateAt: DateTime.UtcNow.AddDays(-100));
        await _fixture.SeedOutboundAsync(inboundId, $"e-{Guid.NewGuid():N}",
            OutboundMessageStatus.Processed, updateAt: DateTime.UtcNow.AddDays(-100));
        await _fixture.SeedOutboundAsync(inboundId, $"e-{Guid.NewGuid():N}",
            OutboundMessageStatus.DeadLettered, updateAt: DateTime.UtcNow.AddDays(-100));

        var worker = BuildCleanupWorker(retentionDays: 90);
        await worker.CleanupOnceAsync(CancellationToken.None);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(0, await verify.Set<InboundMessage>().CountAsync(x => x.Id == inboundId));
        // Cascade dropped the terminal children too (or cleanup already did).
        Assert.Equal(0, await verify.Set<OutboundMessage>().CountAsync(x => x.InboundMessageId == inboundId));
    }

    [Fact]
    public async Task CleanupOnce_DoesNotDeleteOldQueuedInbound_WhenAnyChildIsNonTerminal()
    {
        // The NOT EXISTS guard prevents the FK cascade from destroying active queue work.
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync(InboundMessageStatus.Queued,
            updateAt: DateTime.UtcNow.AddDays(-100));
        await _fixture.SeedOutboundAsync(inboundId, $"e-{Guid.NewGuid():N}",
            OutboundMessageStatus.Processed, updateAt: DateTime.UtcNow.AddDays(-100));
        var activeChildId = await _fixture.SeedOutboundAsync(inboundId, $"e-{Guid.NewGuid():N}",
            OutboundMessageStatus.New, updateAt: DateTime.UtcNow.AddDays(-100));

        var worker = BuildCleanupWorker(retentionDays: 90);
        await worker.CleanupOnceAsync(CancellationToken.None);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verify.Set<InboundMessage>().CountAsync(x => x.Id == inboundId)); // parent untouched
        Assert.Equal(1, await CountOutboundAsync(activeChildId)); // active child still alive
    }

    [Fact]
    public async Task CleanupOnce_DeletesOldQueuedScheduled()
    {
        await _fixture.ResetAsync();

        var oldId = await _fixture.SeedScheduledAsync(ScheduledInboundMessageStatus.Queued,
            updateAt: DateTime.UtcNow.AddDays(-100));

        var worker = BuildCleanupWorker(retentionDays: 90);
        await worker.CleanupOnceAsync(CancellationToken.None);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(0, await verify.Set<ScheduledInboundMessage>().CountAsync(x => x.Id == oldId));
    }

    [Fact]
    public async Task CleanupOnce_DoesNotDeleteWaitingScheduled()
    {
        await _fixture.ResetAsync();

        var waitingId = await _fixture.SeedScheduledAsync(ScheduledInboundMessageStatus.WaitingScheduledTime,
            updateAt: DateTime.UtcNow.AddDays(-100));

        var worker = BuildCleanupWorker(retentionDays: 90);
        await worker.CleanupOnceAsync(CancellationToken.None);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verify.Set<ScheduledInboundMessage>().CountAsync(x => x.Id == waitingId));
    }

    [Fact]
    public async Task CleanupOnce_BatchSizeLimitsDeletesPerSweep()
    {
        await _fixture.ResetAsync();
        // Inbound is RECENT (not eligible for cleanup), so the cascade can't sweep the
        // remaining outbound children when this sweep doesn't get to them via batch size.
        var inboundId = await _fixture.SeedInboundAsync(updateAt: DateTime.UtcNow.AddDays(-1));

        for (var i = 0; i < 5; i++)
        {
            await _fixture.SeedOutboundAsync(inboundId, $"e-{Guid.NewGuid():N}",
                OutboundMessageStatus.Processed, updateAt: DateTime.UtcNow.AddDays(-100));
        }

        var worker = BuildCleanupWorker(retentionDays: 90, batchSize: 2);
        await worker.CleanupOnceAsync(CancellationToken.None);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(3, await verify.Set<OutboundMessage>().CountAsync(x => x.InboundMessageId == inboundId));
    }

    [Fact]
    public async Task CleanupOnce_DoesNotDeleteInboundInNonTerminalStatus()
    {
        // Only Queued is terminal for inbound. New rows must never be cleaned even if very old.
        await _fixture.ResetAsync();

        var newInboundId = await _fixture.SeedInboundAsync(InboundMessageStatus.New,
            updateAt: DateTime.UtcNow.AddDays(-100));

        var worker = BuildCleanupWorker(retentionDays: 90);
        await worker.CleanupOnceAsync(CancellationToken.None);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verify.Set<InboundMessage>().CountAsync(x => x.Id == newInboundId));
    }

    [Fact]
    public async Task CleanupOnce_DoesNotDeleteRecentQueuedInbound()
    {
        await _fixture.ResetAsync();

        var recentId = await _fixture.SeedInboundAsync(InboundMessageStatus.Queued,
            updateAt: DateTime.UtcNow.AddDays(-1));

        var worker = BuildCleanupWorker(retentionDays: 90);
        await worker.CleanupOnceAsync(CancellationToken.None);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verify.Set<InboundMessage>().CountAsync(x => x.Id == recentId));
    }

    [Fact]
    public async Task CleanupOnce_DoesNotDeleteRecentQueuedScheduled()
    {
        await _fixture.ResetAsync();

        var recentId = await _fixture.SeedScheduledAsync(ScheduledInboundMessageStatus.Queued,
            updateAt: DateTime.UtcNow.AddDays(-1));

        var worker = BuildCleanupWorker(retentionDays: 90);
        await worker.CleanupOnceAsync(CancellationToken.None);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verify.Set<ScheduledInboundMessage>().CountAsync(x => x.Id == recentId));
    }

    [Fact]
    public async Task CleanupOnce_UsesCreateAtFallbackWhenUpdateAtIsNull()
    {
        // The cutoff predicate is COALESCE(UpdateAt, CreateAt) < cutoff. With UpdateAt = NULL,
        // CreateAt is the deciding column. We can't easily backdate CreateAt without modifying
        // the row after insert — do that here with a follow-up UPDATE.
        await _fixture.ResetAsync();

        // Seed with UpdateAt = NULL (default). CreateAt is set to now() by the seed.
        // Then backdate CreateAt to 100 days ago via raw SQL.
        var inboundId = await _fixture.SeedInboundAsync(updateAt: DateTime.UtcNow.AddDays(-1)); // recent updateAt — parent stays
        var oldId = await _fixture.SeedOutboundAsync(inboundId, $"e-{Guid.NewGuid():N}",
            OutboundMessageStatus.Processed, updateAt: null);

        await using (var conn = new Npgsql.NpgsqlConnection(_fixture.ConnectionString))
        {
            await conn.OpenAsync();
            await using var update = conn.CreateCommand();
            update.CommandText = """UPDATE mirage_queue."OutboundMessage" SET "CreateAt" = now() - interval '100 days', "UpdateAt" = NULL WHERE "Id" = @id""";
            update.Parameters.Add(new Npgsql.NpgsqlParameter("id", oldId));
            await update.ExecuteNonQueryAsync();
        }

        var worker = BuildCleanupWorker(retentionDays: 90);
        await worker.CleanupOnceAsync(CancellationToken.None);

        // Row deleted via the CreateAt fallback (UpdateAt was NULL).
        Assert.Equal(0, await CountOutboundAsync(oldId));
    }

    [Fact]
    public async Task CleanupOnce_MultipleSweeps_EventuallyClearsBacklog()
    {
        // Verifies the worker behavior across consecutive sweeps: after the first
        // sweep deletes `batchSize` rows, a second sweep finishes the remainder.
        await _fixture.ResetAsync();
        var inboundId = await _fixture.SeedInboundAsync(updateAt: DateTime.UtcNow.AddDays(-1));  // recent — stays

        for (var i = 0; i < 4; i++)
        {
            await _fixture.SeedOutboundAsync(inboundId, $"e-{Guid.NewGuid():N}",
                OutboundMessageStatus.Processed, updateAt: DateTime.UtcNow.AddDays(-100));
        }

        var worker = BuildCleanupWorker(retentionDays: 90, batchSize: 2);

        // First sweep: 2 of 4 deleted.
        await worker.CleanupOnceAsync(CancellationToken.None);
        await using (var afterFirst = _fixture.CreateMirageQueueDbContext())
        {
            Assert.Equal(2, await afterFirst.Set<OutboundMessage>().CountAsync(x => x.InboundMessageId == inboundId));
        }

        // Second sweep: the last 2 deleted.
        await worker.CleanupOnceAsync(CancellationToken.None);
        await using (var afterSecond = _fixture.CreateMirageQueueDbContext())
        {
            Assert.Equal(0, await afterSecond.Set<OutboundMessage>().CountAsync(x => x.InboundMessageId == inboundId));
        }
    }

    [Fact]
    public async Task CleanupWorker_WhenDisabled_DoesNotDeleteAnything()
    {
        await _fixture.ResetAsync();
        var inboundId = await _fixture.SeedInboundAsync(updateAt: DateTime.UtcNow.AddDays(-100));
        var oldId = await _fixture.SeedOutboundAsync(inboundId, $"e-{Guid.NewGuid():N}",
            OutboundMessageStatus.Processed, updateAt: DateTime.UtcNow.AddDays(-100));

        // With CleanupEnabled = false, ExecuteAsync returns immediately (no awaits before the
        // early-return path), so StartAsync's awaited task is already complete by the time it
        // returns — no sleep needed to verify no sweep occurred.
        var worker = BuildCleanupWorker(retentionDays: 90, cleanupEnabled: false);
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, await CountOutboundAsync(oldId));
    }

    private PgMessageCleanupWorker BuildCleanupWorker(int retentionDays, int batchSize = 1000, bool cleanupEnabled = true)
    {
        var config = new MirageQueueConfiguration
        {
            CleanupEnabled = cleanupEnabled,
            MessageRetentionDays = retentionDays,
            CleanupBatchSize = batchSize,
            CleanupPollingTime = 86_400_000,
        };
        return new PgMessageCleanupWorker(
            _fixture.Services,
            NullLogger<MessageCleanupWorker>.Instance,
            config);
    }

    private async Task<int> CountOutboundAsync(Guid id)
    {
        await using var verify = _fixture.CreateMirageQueueDbContext();
        return await verify.Set<OutboundMessage>().CountAsync(x => x.Id == id);
    }
}

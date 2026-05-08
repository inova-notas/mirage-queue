using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.IntegrationTests.Fixtures;
using MirageQueue.Messages.Entities;
using MirageQueue.Outbox;
using Xunit;

namespace MirageQueue.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class DbContextOutboxTests
{
    private readonly PostgresFixture _fixture;

    public DbContextOutboxTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveChangesAndFlush_NoAmbientTransaction_OpensCommitsAndPersistsBoth()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var business = scope.ServiceProvider.GetRequiredService<SampleBusinessDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<SampleBusinessDbContext>>();

        business.SampleEntities.Add(new SampleBusinessEntity { Id = Guid.NewGuid(), Name = "ambient-none" });
        outbox.Publish(new TestMessage { Id = Guid.NewGuid() });

        await outbox.SaveChangesAndFlushMessagesAsync();

        await using var verifyMq = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verifyMq.Set<InboundMessage>().CountAsync());

        await using var verifyBiz = _fixture.CreateBusinessDbContext();
        Assert.Equal(1, await verifyBiz.SampleEntities.CountAsync());
    }

    [Fact]
    public async Task SaveChangesAndFlush_WithAmbientTransaction_DoesNotCommitUntilCallerCommits()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var business = scope.ServiceProvider.GetRequiredService<SampleBusinessDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<SampleBusinessDbContext>>();

        await using var transaction = await business.Database.BeginTransactionAsync();

        business.SampleEntities.Add(new SampleBusinessEntity { Id = Guid.NewGuid(), Name = "ambient-yes" });
        outbox.Publish(new TestMessage { Id = Guid.NewGuid() });
        await outbox.SaveChangesAndFlushMessagesAsync();

        // Before caller commits: a separate connection should see nothing yet (READ COMMITTED).
        await using (var preCommitMq = _fixture.CreateMirageQueueDbContext())
        {
            Assert.Equal(0, await preCommitMq.Set<InboundMessage>().CountAsync());
        }

        await transaction.CommitAsync();

        await using var verifyMq = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verifyMq.Set<InboundMessage>().CountAsync());

        await using var verifyBiz = _fixture.CreateBusinessDbContext();
        Assert.Equal(1, await verifyBiz.SampleEntities.CountAsync());
    }

    [Fact]
    public async Task SaveChangesAndFlush_NoAmbient_BusinessSaveFails_RollsBackQueueWrites()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var business = scope.ServiceProvider.GetRequiredService<SampleBusinessDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<SampleBusinessDbContext>>();

        // Force business SaveChanges to fail by violating the Name length constraint.
        business.SampleEntities.Add(new SampleBusinessEntity { Id = Guid.NewGuid(), Name = new string('x', 1000) });
        outbox.Publish(new TestMessage { Id = Guid.NewGuid() });

        await Assert.ThrowsAsync<DbUpdateException>(() => outbox.SaveChangesAndFlushMessagesAsync());

        await using var verifyMq = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(0, await verifyMq.Set<InboundMessage>().CountAsync());

        await using var verifyBiz = _fixture.CreateBusinessDbContext();
        Assert.Equal(0, await verifyBiz.SampleEntities.CountAsync());
    }

    [Fact]
    public async Task SaveChangesAndFlush_WithSchedule_NoAmbient_PersistsScheduled()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var business = scope.ServiceProvider.GetRequiredService<SampleBusinessDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<SampleBusinessDbContext>>();

        business.SampleEntities.Add(new SampleBusinessEntity { Id = Guid.NewGuid(), Name = "schedule-only" });
        outbox.Schedule(new TestMessage { Id = Guid.NewGuid() }, DateTime.UtcNow.AddMinutes(5));

        await outbox.SaveChangesAndFlushMessagesAsync();

        await using var verifyMq = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(0, await verifyMq.Set<InboundMessage>().CountAsync());
        Assert.Equal(1, await verifyMq.Set<ScheduledInboundMessage>().CountAsync());

        await using var verifyBiz = _fixture.CreateBusinessDbContext();
        Assert.Equal(1, await verifyBiz.SampleEntities.CountAsync());
    }

    [Fact]
    public async Task SaveChangesAndFlush_MixedPublishAndSchedule_PersistsBoth()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var business = scope.ServiceProvider.GetRequiredService<SampleBusinessDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<SampleBusinessDbContext>>();

        business.SampleEntities.Add(new SampleBusinessEntity { Id = Guid.NewGuid(), Name = "mixed" });
        outbox.Publish(new TestMessage { Id = Guid.NewGuid() });
        outbox.Publish(new TestMessage { Id = Guid.NewGuid() });
        outbox.Schedule(new TestMessage { Id = Guid.NewGuid() }, DateTime.UtcNow.AddMinutes(10));

        await outbox.SaveChangesAndFlushMessagesAsync();

        await using var verifyMq = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(2, await verifyMq.Set<InboundMessage>().CountAsync());
        Assert.Equal(1, await verifyMq.Set<ScheduledInboundMessage>().CountAsync());

        await using var verifyBiz = _fixture.CreateBusinessDbContext();
        Assert.Equal(1, await verifyBiz.SampleEntities.CountAsync());
    }

    [Fact]
    public async Task SaveChangesAndFlush_WithAmbientTransaction_CallerRollsBack_BothDiscarded()
    {
        // Companion to SaveChangesAndFlush_WithAmbientTransaction_DoesNotCommitUntilCallerCommits.
        // That test only proves uncommitted state isn't visible from a separate connection.
        // This one proves the outbox actually respects an explicit RollbackAsync on a caller-owned tx —
        // both business and queue writes vanish for good.
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var business = scope.ServiceProvider.GetRequiredService<SampleBusinessDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<SampleBusinessDbContext>>();

        await using var transaction = await business.Database.BeginTransactionAsync();

        business.SampleEntities.Add(new SampleBusinessEntity { Id = Guid.NewGuid(), Name = "explicit-rollback" });
        outbox.Publish(new TestMessage { Id = Guid.NewGuid() });
        outbox.Schedule(new TestMessage { Id = Guid.NewGuid() }, DateTime.UtcNow.AddMinutes(5));

        // Outbox flushed but did NOT commit — caller owns the tx.
        await outbox.SaveChangesAndFlushMessagesAsync();

        // Caller explicitly rolls back. Outbox must not have silently committed; everything gone.
        await transaction.RollbackAsync();

        await using var verifyMq = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(0, await verifyMq.Set<InboundMessage>().CountAsync());
        Assert.Equal(0, await verifyMq.Set<ScheduledInboundMessage>().CountAsync());

        await using var verifyBiz = _fixture.CreateBusinessDbContext();
        Assert.Equal(0, await verifyBiz.SampleEntities.CountAsync());
    }

    [Fact]
    public async Task SaveChangesAndFlush_WithSchedule_BusinessSaveFails_BothRolledBack()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var business = scope.ServiceProvider.GetRequiredService<SampleBusinessDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<SampleBusinessDbContext>>();

        business.SampleEntities.Add(new SampleBusinessEntity { Id = Guid.NewGuid(), Name = new string('x', 1000) });
        outbox.Schedule(new TestMessage { Id = Guid.NewGuid() }, DateTime.UtcNow.AddMinutes(5));

        await Assert.ThrowsAsync<DbUpdateException>(() => outbox.SaveChangesAndFlushMessagesAsync());

        await using var verifyMq = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(0, await verifyMq.Set<ScheduledInboundMessage>().CountAsync());

        await using var verifyBiz = _fixture.CreateBusinessDbContext();
        Assert.Equal(0, await verifyBiz.SampleEntities.CountAsync());
    }
}

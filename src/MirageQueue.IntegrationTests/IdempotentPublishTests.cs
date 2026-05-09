using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.IntegrationTests.Fixtures;
using MirageQueue.Messages.Entities;
using MirageQueue.Postgres.Databases;
using MirageQueue.Publishers.Abstractions;
using Xunit;

namespace MirageQueue.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class IdempotentPublishTests
{
    private readonly PostgresFixture _fixture;

    public IdempotentPublishTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Publish_WithIdempotencyKey_FirstCallReturnsIsDuplicateFalse()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var result = await publisher.Publish(new TestMessage { Id = Guid.NewGuid() }, "order-001");

        Assert.False(result.IsDuplicate);
        Assert.NotEqual(Guid.Empty, result.MessageId);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verify.Set<InboundMessage>().CountAsync());
    }

    [Fact]
    public async Task Publish_WithSameIdempotencyKeyTwice_SecondCallReturnsIsDuplicateTrueAndSameId()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var first = await publisher.Publish(new TestMessage { Id = Guid.NewGuid() }, "order-002");
        var second = await publisher.Publish(new TestMessage { Id = Guid.NewGuid() }, "order-002");

        Assert.False(first.IsDuplicate);
        Assert.True(second.IsDuplicate);
        Assert.Equal(first.MessageId, second.MessageId);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verify.Set<InboundMessage>().CountAsync());
    }

    [Fact]
    public async Task Publish_WithDifferentIdempotencyKeys_ProducesTwoRows()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var a = await publisher.Publish(new TestMessage { Id = Guid.NewGuid() }, "order-a");
        var b = await publisher.Publish(new TestMessage { Id = Guid.NewGuid() }, "order-b");

        Assert.False(a.IsDuplicate);
        Assert.False(b.IsDuplicate);
        Assert.NotEqual(a.MessageId, b.MessageId);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(2, await verify.Set<InboundMessage>().CountAsync());
    }

    [Fact]
    public async Task Publish_WithoutIdempotencyKey_AlwaysProducesNewRow()
    {
        // Regression: the partial unique index excludes NULL keys, so unkeyed
        // publishes must NOT dedup against each other.
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        await publisher.Publish(new TestMessage { Id = Guid.NewGuid() });
        await publisher.Publish(new TestMessage { Id = Guid.NewGuid() });
        await publisher.Publish(new TestMessage { Id = Guid.NewGuid() });

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(3, await verify.Set<InboundMessage>().CountAsync());
    }

    [Fact]
    public async Task Publish_MixedNullAndKeyed_PartialIndexIgnoresNullsAndDedupsKeyedOnly()
    {
        // Comprehensive test of the partial unique index semantics, all in one go:
        //   - unkeyed publishes coexist (NULL keys are not subject to the index)
        //   - keyed publishes dedup against themselves (same key → IsDuplicate=true)
        //   - keyed publishes with different keys are independent
        //   - mixing the two doesn't corrupt either path
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        // Three unkeyed publishes — all should insert (NULL key, partial index ignores).
        await publisher.Publish(new TestMessage { Id = Guid.NewGuid() });
        await publisher.Publish(new TestMessage { Id = Guid.NewGuid() });
        await publisher.Publish(new TestMessage { Id = Guid.NewGuid() });

        // Keyed publish with key "A" — first insert.
        var firstA = await publisher.Publish(new TestMessage { Id = Guid.NewGuid() }, "key-A");
        Assert.False(firstA.IsDuplicate);

        // Another unkeyed publish — must still insert despite a keyed row now existing.
        await publisher.Publish(new TestMessage { Id = Guid.NewGuid() });

        // Repeat key "A" — should dedup, return same id.
        var secondA = await publisher.Publish(new TestMessage { Id = Guid.NewGuid() }, "key-A");
        Assert.True(secondA.IsDuplicate);
        Assert.Equal(firstA.MessageId, secondA.MessageId);

        // Keyed publish with key "B" — separate from "A".
        var firstB = await publisher.Publish(new TestMessage { Id = Guid.NewGuid() }, "key-B");
        Assert.False(firstB.IsDuplicate);
        Assert.NotEqual(firstA.MessageId, firstB.MessageId);

        // One more unkeyed — proves the unkeyed path still works after multiple keyed inserts.
        await publisher.Publish(new TestMessage { Id = Guid.NewGuid() });

        await using var verify = _fixture.CreateMirageQueueDbContext();

        // 5 unkeyed inserts + 2 distinct keyed inserts ("A" + "B") = 7 rows.
        Assert.Equal(7, await verify.Set<InboundMessage>().CountAsync());

        // Exactly 5 NULL-keyed rows.
        Assert.Equal(5, await verify.Set<InboundMessage>().CountAsync(x => x.IdempotencyKey == null));

        // Exactly 2 non-NULL-keyed rows.
        Assert.Equal(2, await verify.Set<InboundMessage>().CountAsync(x => x.IdempotencyKey != null));
    }

    [Fact]
    public async Task Schedule_MixedNullAndKeyed_PartialIndexIgnoresNullsAndDedupsKeyedOnly()
    {
        // Same comprehensive coverage but for ScheduledInboundMessage.
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var time = DateTime.UtcNow.AddMinutes(5);

        await publisher.Schedule(new TestMessage { Id = Guid.NewGuid() }, time);
        await publisher.Schedule(new TestMessage { Id = Guid.NewGuid() }, time);

        var firstX = await publisher.Schedule(new TestMessage { Id = Guid.NewGuid() }, time, "schedule-X");
        Assert.False(firstX.IsDuplicate);

        await publisher.Schedule(new TestMessage { Id = Guid.NewGuid() }, time);

        var secondX = await publisher.Schedule(new TestMessage { Id = Guid.NewGuid() }, time, "schedule-X");
        Assert.True(secondX.IsDuplicate);
        Assert.Equal(firstX.MessageId, secondX.MessageId);

        var firstY = await publisher.Schedule(new TestMessage { Id = Guid.NewGuid() }, time, "schedule-Y");
        Assert.False(firstY.IsDuplicate);

        await using var verify = _fixture.CreateMirageQueueDbContext();

        // 3 unkeyed + 2 distinct keyed = 5 rows.
        Assert.Equal(5, await verify.Set<ScheduledInboundMessage>().CountAsync());
        Assert.Equal(3, await verify.Set<ScheduledInboundMessage>().CountAsync(x => x.IdempotencyKey == null));
        Assert.Equal(2, await verify.Set<ScheduledInboundMessage>().CountAsync(x => x.IdempotencyKey != null));
    }

    [Fact]
    public async Task Schedule_WithSameIdempotencyKeyTwice_SecondCallReturnsIsDuplicateTrueAndSameId()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var time = DateTime.UtcNow.AddMinutes(5);
        var first = await publisher.Schedule(new TestMessage { Id = Guid.NewGuid() }, time, "reminder-7");
        var second = await publisher.Schedule(new TestMessage { Id = Guid.NewGuid() }, time, "reminder-7");

        Assert.False(first.IsDuplicate);
        Assert.True(second.IsDuplicate);
        Assert.Equal(first.MessageId, second.MessageId);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verify.Set<ScheduledInboundMessage>().CountAsync());
    }

    [Fact]
    public async Task Publish_WithIdempotencyKey_AndDbTransaction_HonorsBoth()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MirageQueueDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var first = await publisher.Publish(new TestMessage { Id = Guid.NewGuid() }, "order-tx", transaction.GetDbTransaction());
        var second = await publisher.Publish(new TestMessage { Id = Guid.NewGuid() }, "order-tx", transaction.GetDbTransaction());

        Assert.False(first.IsDuplicate);
        Assert.True(second.IsDuplicate);
        Assert.Equal(first.MessageId, second.MessageId);

        await transaction.CommitAsync();

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verify.Set<InboundMessage>().CountAsync());
    }

    [Fact]
    public async Task Publish_WithIdempotencyKey_AndRolledBackTransaction_PersistsNothing()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MirageQueueDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        await publisher.Publish(new TestMessage { Id = Guid.NewGuid() }, "order-rb", transaction.GetDbTransaction());
        await transaction.RollbackAsync();

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(0, await verify.Set<InboundMessage>().CountAsync());
    }
}

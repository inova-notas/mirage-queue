using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.IntegrationTests.Fixtures;
using MirageQueue.Messages.Entities;
using MirageQueue.Outbox;
using Xunit;

namespace MirageQueue.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class OutboxIdempotentTests
{
    private readonly PostgresFixture _fixture;

    public OutboxIdempotentTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveChangesAndFlush_ReturnsOnePublishResultPerBufferedPublish()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var business = scope.ServiceProvider.GetRequiredService<SampleBusinessDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<SampleBusinessDbContext>>();

        business.SampleEntities.Add(new SampleBusinessEntity { Id = Guid.NewGuid(), Name = "three-publishes" });
        outbox.Publish(new TestMessage { Id = Guid.NewGuid() });
        outbox.Publish(new TestMessage { Id = Guid.NewGuid() }, "key-1");
        outbox.Schedule(new TestMessage { Id = Guid.NewGuid() }, DateTime.UtcNow.AddMinutes(5));

        var results = await outbox.SaveChangesAndFlushMessagesAsync();

        Assert.Equal(3, results.Count);
        Assert.False(results[0].IsDuplicate);
        Assert.False(results[1].IsDuplicate);
        Assert.False(results[2].IsDuplicate);

        // Unkeyed publishes have null MessageId; the keyed one carries the inbound row's Id.
        Assert.Null(results[0].MessageId);
        Assert.NotNull(results[1].MessageId);
        Assert.Null(results[2].MessageId);
    }

    [Fact]
    public async Task SaveChangesAndFlush_DuplicateKeyAcrossFlushes_SecondFlushReturnsIsDuplicateTrue()
    {
        await _fixture.ResetAsync();

        // First flush
        Guid? firstMessageId;
        {
            await using var scope = _fixture.Services.CreateAsyncScope();
            var business = scope.ServiceProvider.GetRequiredService<SampleBusinessDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<SampleBusinessDbContext>>();

            business.SampleEntities.Add(new SampleBusinessEntity { Id = Guid.NewGuid(), Name = "first" });
            outbox.Publish(new TestMessage { Id = Guid.NewGuid() }, "shared-key");

            var results = await outbox.SaveChangesAndFlushMessagesAsync();
            firstMessageId = results.Single().MessageId;
            Assert.NotNull(firstMessageId);
            Assert.False(results.Single().IsDuplicate);
        }

        // Second flush — same key
        {
            await using var scope = _fixture.Services.CreateAsyncScope();
            var business = scope.ServiceProvider.GetRequiredService<SampleBusinessDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<SampleBusinessDbContext>>();

            business.SampleEntities.Add(new SampleBusinessEntity { Id = Guid.NewGuid(), Name = "second" });
            outbox.Publish(new TestMessage { Id = Guid.NewGuid() }, "shared-key");

            var results = await outbox.SaveChangesAndFlushMessagesAsync();
            Assert.True(results.Single().IsDuplicate);
            Assert.Equal(firstMessageId, results.Single().MessageId);
        }

        await using var verifyMq = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verifyMq.Set<InboundMessage>().CountAsync());

        // The two business rows still exist independently — outbox dedup is on queue rows only.
        await using var verifyBiz = _fixture.CreateBusinessDbContext();
        Assert.Equal(2, await verifyBiz.SampleEntities.CountAsync());
    }

    [Fact]
    public async Task SaveChangesAndFlush_MixedKeyedAndUnkeyed_ReturnsCorrectFlagsForEach()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var business = scope.ServiceProvider.GetRequiredService<SampleBusinessDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<SampleBusinessDbContext>>();

        // Pre-populate one duplicate target via the publisher directly, in a separate scope.
        await using (var preScope = _fixture.Services.CreateAsyncScope())
        {
            var publisher = preScope.ServiceProvider.GetRequiredService<MirageQueue.Publishers.Abstractions.IPublisher>();
            await publisher.Publish(new TestMessage { Id = Guid.NewGuid() }, "pre-existing-key");
        }

        business.SampleEntities.Add(new SampleBusinessEntity { Id = Guid.NewGuid(), Name = "mixed" });
        outbox.Publish(new TestMessage { Id = Guid.NewGuid() });                      // unkeyed
        outbox.Publish(new TestMessage { Id = Guid.NewGuid() }, "fresh-key");         // keyed, new
        outbox.Publish(new TestMessage { Id = Guid.NewGuid() }, "pre-existing-key");  // keyed, duplicate

        var results = await outbox.SaveChangesAndFlushMessagesAsync();

        Assert.Equal(3, results.Count);
        Assert.False(results[0].IsDuplicate);  // unkeyed
        Assert.False(results[1].IsDuplicate);  // fresh keyed
        Assert.True(results[2].IsDuplicate);   // pre-existing keyed

        await using var verifyMq = _fixture.CreateMirageQueueDbContext();
        // Pre-existing + unkeyed + fresh-keyed = 3 inbound rows; the duplicate didn't insert a new one.
        Assert.Equal(3, await verifyMq.Set<InboundMessage>().CountAsync());
    }
}

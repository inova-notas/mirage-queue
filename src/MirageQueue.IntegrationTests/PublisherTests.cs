using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.IntegrationTests.Fixtures;
using MirageQueue.Messages.Entities;
using MirageQueue.Publishers.Abstractions;
using Xunit;

namespace MirageQueue.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class PublisherTests
{
    private readonly PostgresFixture _fixture;

    public PublisherTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Publish_WithoutTransaction_PersistsInboundMessage()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        await publisher.Publish(new TestMessage { Id = Guid.NewGuid() });

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var count = await verify.Set<InboundMessage>().CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Schedule_WithoutTransaction_PersistsScheduledInboundMessage()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        await publisher.Schedule(new TestMessage { Id = Guid.NewGuid() }, DateTime.UtcNow.AddMinutes(5));

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var count = await verify.Set<ScheduledInboundMessage>().CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Publish_WithoutTransaction_CommitsIndependentlyOfCallerTransaction()
    {
        // Documents the contract: the non-transactional overload uses its own connection
        // and commits regardless of any transaction the caller has open on a different context.
        // This is exactly the "ghost publish" pitfall the transactional overload exists to fix.
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var business = scope.ServiceProvider.GetRequiredService<SampleBusinessDbContext>();

        await using var transaction = await business.Database.BeginTransactionAsync();

        business.SampleEntities.Add(new SampleBusinessEntity { Id = Guid.NewGuid(), Name = "ghost-publish" });
        await business.SaveChangesAsync();

        // Non-transactional publish — uses its own connection, NOT the caller's transaction.
        await publisher.Publish(new TestMessage { Id = Guid.NewGuid() });

        // Caller rolls back. The business row is gone, but the publish persisted.
        await transaction.RollbackAsync();

        await using var verifyMq = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verifyMq.Set<InboundMessage>().CountAsync());

        await using var verifyBiz = _fixture.CreateBusinessDbContext();
        Assert.Equal(0, await verifyBiz.SampleEntities.CountAsync());
    }

    [Fact]
    public async Task Schedule_WithoutTransaction_CommitsIndependentlyOfCallerTransaction()
    {
        // Same contract as Publish_WithoutTransaction_CommitsIndependentlyOfCallerTransaction
        // but for the Schedule overload — proves Schedule shares the same ghost-publish behavior.
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var business = scope.ServiceProvider.GetRequiredService<SampleBusinessDbContext>();

        await using var transaction = await business.Database.BeginTransactionAsync();

        business.SampleEntities.Add(new SampleBusinessEntity { Id = Guid.NewGuid(), Name = "ghost-schedule" });
        await business.SaveChangesAsync();

        await publisher.Schedule(new TestMessage { Id = Guid.NewGuid() }, DateTime.UtcNow.AddMinutes(5));

        await transaction.RollbackAsync();

        await using var verifyMq = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verifyMq.Set<ScheduledInboundMessage>().CountAsync());

        await using var verifyBiz = _fixture.CreateBusinessDbContext();
        Assert.Equal(0, await verifyBiz.SampleEntities.CountAsync());
    }
}

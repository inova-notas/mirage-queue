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
public class PublisherTransactionalTests
{
    private readonly PostgresFixture _fixture;

    public PublisherTransactionalTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Publish_WithCommittedTransaction_PersistsInbound()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MirageQueueDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        await publisher.Publish(new TestMessage { Id = Guid.NewGuid() }, transaction.GetDbTransaction());
        await transaction.CommitAsync();

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var count = await verify.Set<InboundMessage>().CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Publish_WithRolledBackTransaction_DoesNotPersistInbound()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MirageQueueDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        await publisher.Publish(new TestMessage { Id = Guid.NewGuid() }, transaction.GetDbTransaction());
        await transaction.RollbackAsync();

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var count = await verify.Set<InboundMessage>().CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Schedule_WithCommittedTransaction_PersistsScheduled()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MirageQueueDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        await publisher.Schedule(
            new TestMessage { Id = Guid.NewGuid() },
            DateTime.UtcNow.AddMinutes(5),
            transaction.GetDbTransaction());
        await transaction.CommitAsync();

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var count = await verify.Set<ScheduledInboundMessage>().CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Schedule_WithRolledBackTransaction_DoesNotPersistScheduled()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MirageQueueDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        await publisher.Schedule(
            new TestMessage { Id = Guid.NewGuid() },
            DateTime.UtcNow.AddMinutes(5),
            transaction.GetDbTransaction());
        await transaction.RollbackAsync();

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var count = await verify.Set<ScheduledInboundMessage>().CountAsync();
        Assert.Equal(0, count);
    }
}

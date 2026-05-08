using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.IntegrationTests.Fixtures;
using MirageQueue.Messages.Entities;
using MirageQueue.Outbox;
using MirageQueue.Postgres.Databases;
using MirageQueue.Publishers.Abstractions;
using Npgsql;
using Xunit;

namespace MirageQueue.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class AtomicityTests
{
    private readonly PostgresFixture _fixture;

    public AtomicityTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BothWritesShareSingleTransaction()
    {
        await _fixture.ResetAsync();

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using (var insertBiz = conn.CreateCommand())
        {
            insertBiz.Transaction = tx;
            insertBiz.CommandText = $"INSERT INTO {SampleBusinessDbContext.SchemaName}.\"SampleEntity\" (\"Id\", \"Name\") VALUES (@id, @name)";
            insertBiz.Parameters.Add(new NpgsqlParameter("id", Guid.NewGuid()));
            insertBiz.Parameters.Add(new NpgsqlParameter("name", "single-tx"));
            await insertBiz.ExecuteNonQueryAsync();
        }

        var xidAfterBusiness = await ReadXidAsync(conn, tx);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        await publisher.Publish(new TestMessage { Id = Guid.NewGuid() }, tx);

        var xidAfterQueue = await ReadXidAsync(conn, tx);

        Assert.Equal(xidAfterBusiness, xidAfterQueue);

        await tx.CommitAsync();

        await using var verifyMq = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verifyMq.Set<InboundMessage>().CountAsync());

        await using var verifyBiz = _fixture.CreateBusinessDbContext();
        Assert.Equal(1, await verifyBiz.SampleEntities.CountAsync());
    }

    [Fact]
    public async Task BusinessSaveFails_BothRolledBack()
    {
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var business = scope.ServiceProvider.GetRequiredService<SampleBusinessDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<SampleBusinessDbContext>>();

        // Name max length is 200 — over 200 chars triggers DbUpdateException on SaveChanges.
        business.SampleEntities.Add(new SampleBusinessEntity { Id = Guid.NewGuid(), Name = new string('x', 1000) });
        outbox.Publish(new TestMessage { Id = Guid.NewGuid() });

        await Assert.ThrowsAsync<DbUpdateException>(() => outbox.SaveChangesAndFlushMessagesAsync());

        await using var verifyMq = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(0, await verifyMq.Set<InboundMessage>().CountAsync());

        await using var verifyBiz = _fixture.CreateBusinessDbContext();
        Assert.Equal(0, await verifyBiz.SampleEntities.CountAsync());
    }

    [Fact]
    public async Task QueueInsertFails_BothRolledBack()
    {
        await _fixture.ResetAsync();

        await using var business = _fixture.CreateBusinessDbContext();
        var outbox = new DbContextOutbox<SampleBusinessDbContext>(business, new ThrowingPublisher());

        business.SampleEntities.Add(new SampleBusinessEntity { Id = Guid.NewGuid(), Name = "queue-fails" });
        outbox.Publish(new TestMessage { Id = Guid.NewGuid() });

        await Assert.ThrowsAsync<InvalidOperationException>(() => outbox.SaveChangesAndFlushMessagesAsync());

        await using var verifyBiz = _fixture.CreateBusinessDbContext();
        Assert.Equal(0, await verifyBiz.SampleEntities.CountAsync());
    }

    private static async Task<long> ReadXidAsync(NpgsqlConnection conn, NpgsqlTransaction tx)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT pg_current_xact_id()::text::bigint";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    private sealed class ThrowingPublisher : IPublisher
    {
        public Task Publish<TMessage>(TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class => throw new InvalidOperationException("simulated publisher failure");

        public Task Publish<TMessage>(TMessage message, DbTransaction transaction, CancellationToken cancellationToken = default)
            where TMessage : class => throw new InvalidOperationException("simulated publisher failure");

        public Task Schedule<TMessage>(TMessage message, DateTime scheduledTime, CancellationToken cancellationToken = default)
            where TMessage : class => throw new InvalidOperationException("simulated publisher failure");

        public Task Schedule<TMessage>(TMessage message, DateTime scheduledTime, DbTransaction transaction, CancellationToken cancellationToken = default)
            where TMessage : class => throw new InvalidOperationException("simulated publisher failure");
    }
}

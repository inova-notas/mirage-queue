using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using MirageQueue.Publishers.Abstractions;
using Npgsql;

namespace MirageQueue.Postgres.Databases;

public class InboundMessageRepository : BaseRepository<MirageQueueDbContext, InboundMessage>, IInboundMessageRepository
{
    readonly MirageQueueDbContext _dbContext;
    readonly NpgsqlParameter _statusParam;
    
    public InboundMessageRepository(MirageQueueDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
        _statusParam = new NpgsqlParameter("statusParam", DbType.Int32)
        {
            Value = (int)InboundMessageStatus.New
        };
    }

    public async Task<List<InboundMessage>> GetQueuedMessages(IDbContextTransaction? transaction = null, int limit = 10)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());
        
        return await _dbContext.Set<InboundMessage>()
            .FromSql($"SELECT * FROM mirage_queue.\"InboundMessage\" WHERE \"Status\" = {_statusParam} FOR UPDATE SKIP LOCKED LIMIT {limit}")
            .AsNoTracking()
            .ToListAsync();
    }
    
    public async Task UpdateMessageStatus(Guid id, InboundMessageStatus status, IDbContextTransaction? transaction = default)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var idParam = new NpgsqlParameter("idParam", id);
        var statusUpdateParam = new NpgsqlParameter("statusUpdateParam", (int)status);
        var updatedParam = new NpgsqlParameter("updatedParam", DateTime.UtcNow);

        await _dbContext.Database.ExecuteSqlAsync($"UPDATE mirage_queue.\"InboundMessage\" SET \"Status\" = {statusUpdateParam}, \"UpdateAt\" = {updatedParam} WHERE \"Id\" = {idParam}");
    }

    public async Task InsertDirect(InboundMessage message, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.Connection is null)
            throw new InvalidOperationException("The supplied transaction is not associated with a connection.");

        await using var cmd = transaction.Connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO mirage_queue."InboundMessage"
                ("Id", "Status", "Content", "MessageContract", "CreateAt", "UpdateAt", "IdempotencyKey", "TraceParent", "TraceState")
            VALUES
                (@id, @status, @content::jsonb, @contract, @createAt, @updateAt, @idempotencyKey, @traceParent, @traceState)
            """;
        cmd.Parameters.Add(new NpgsqlParameter("id", message.Id));
        cmd.Parameters.Add(new NpgsqlParameter("status", (int)message.Status));
        cmd.Parameters.Add(new NpgsqlParameter("content", message.Content));
        cmd.Parameters.Add(new NpgsqlParameter("contract", message.MessageContract));
        cmd.Parameters.Add(new NpgsqlParameter("createAt", message.CreateAt));
        cmd.Parameters.Add(new NpgsqlParameter("updateAt", (object?)message.UpdateAt ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("idempotencyKey", (object?)message.IdempotencyKey ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("traceParent", (object?)message.TraceParent ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("traceState", (object?)message.TraceState ?? DBNull.Value));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PublishResult> InsertIfNotExists(InboundMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrEmpty(message.IdempotencyKey))
            throw new InvalidOperationException($"{nameof(InsertIfNotExists)} requires an {nameof(InboundMessage.IdempotencyKey)}.");

        var conn = (NpgsqlConnection)_dbContext.Database.GetDbConnection();
        var connectionWasClosed = conn.State == ConnectionState.Closed;
        if (connectionWasClosed)
            await conn.OpenAsync(cancellationToken);

        try
        {
            return await ExecuteInsertIfNotExistsAsync(message, conn, transaction: null, cancellationToken);
        }
        finally
        {
            if (connectionWasClosed)
                await conn.CloseAsync();
        }
    }

    public async Task<PublishResult> InsertDirectIfNotExists(InboundMessage message, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(transaction);

        if (string.IsNullOrEmpty(message.IdempotencyKey))
            throw new InvalidOperationException($"{nameof(InsertDirectIfNotExists)} requires an {nameof(InboundMessage.IdempotencyKey)}.");

        if (transaction.Connection is null)
            throw new InvalidOperationException("The supplied transaction is not associated with a connection.");

        return await ExecuteInsertIfNotExistsAsync(message, transaction.Connection, transaction, cancellationToken);
    }

    private static async Task<PublishResult> ExecuteInsertIfNotExistsAsync(
        InboundMessage message,
        DbConnection conn,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using (var insertCmd = conn.CreateCommand())
        {
            if (transaction is not null) insertCmd.Transaction = transaction;
            insertCmd.CommandText = """
                INSERT INTO mirage_queue."InboundMessage"
                    ("Id", "Status", "Content", "MessageContract", "CreateAt", "UpdateAt", "IdempotencyKey", "TraceParent", "TraceState")
                VALUES
                    (@id, @status, @content::jsonb, @contract, @createAt, @updateAt, @idempotencyKey, @traceParent, @traceState)
                ON CONFLICT ("IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL DO NOTHING
                RETURNING "Id"
                """;
            insertCmd.Parameters.Add(new NpgsqlParameter("id", message.Id));
            insertCmd.Parameters.Add(new NpgsqlParameter("status", (int)message.Status));
            insertCmd.Parameters.Add(new NpgsqlParameter("content", message.Content));
            insertCmd.Parameters.Add(new NpgsqlParameter("contract", message.MessageContract));
            insertCmd.Parameters.Add(new NpgsqlParameter("createAt", message.CreateAt));
            insertCmd.Parameters.Add(new NpgsqlParameter("updateAt", (object?)message.UpdateAt ?? DBNull.Value));
            insertCmd.Parameters.Add(new NpgsqlParameter("idempotencyKey", message.IdempotencyKey!));
            insertCmd.Parameters.Add(new NpgsqlParameter("traceParent", (object?)message.TraceParent ?? DBNull.Value));
            insertCmd.Parameters.Add(new NpgsqlParameter("traceState", (object?)message.TraceState ?? DBNull.Value));

            var insertedId = await insertCmd.ExecuteScalarAsync(cancellationToken);
            if (insertedId is Guid newId)
                return new PublishResult(newId, IsDuplicate: false);
        }

        // Conflict — fetch the existing row's Id by IdempotencyKey.
        await using var selectCmd = conn.CreateCommand();
        if (transaction is not null) selectCmd.Transaction = transaction;
        selectCmd.CommandText = """SELECT "Id" FROM mirage_queue."InboundMessage" WHERE "IdempotencyKey" = @key""";
        selectCmd.Parameters.Add(new NpgsqlParameter("key", message.IdempotencyKey!));

        var existingId = (Guid)(await selectCmd.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Idempotency key conflict but no existing row found — race condition?"));

        return new PublishResult(existingId, IsDuplicate: true);
    }

    public async Task<int> DeleteQueuedOlderThanWithNoActiveOutbound(DateTime cutoff, int batchSize, IDbContextTransaction? transaction = null)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var queuedParam = new NpgsqlParameter("queuedParam", (int)InboundMessageStatus.Queued);
        var processedParam = new NpgsqlParameter("processedParam", (int)OutboundMessageStatus.Processed);
        var deadLetteredParam = new NpgsqlParameter("deadLetteredParam", (int)OutboundMessageStatus.DeadLettered);
        var cutoffParam = new NpgsqlParameter("cutoffParam", cutoff);
        var batchParam = new NpgsqlParameter("batchParam", batchSize);

        // The NOT EXISTS clause prevents the FK cascade from destroying outbound rows that
        // are still in non-terminal states (New / Processing / Failed). We only delete the
        // inbound parent if every remaining child is itself a terminal-cleanable status.
        return await _dbContext.Database.ExecuteSqlAsync(
            $"""
            DELETE FROM mirage_queue."InboundMessage"
            WHERE "Id" IN (
                SELECT i."Id" FROM mirage_queue."InboundMessage" i
                WHERE i."Status" = {queuedParam}
                  AND COALESCE(i."UpdateAt", i."CreateAt") < {cutoffParam}
                  AND NOT EXISTS (
                      SELECT 1 FROM mirage_queue."OutboundMessage" o
                      WHERE o."InboundMessageId" = i."Id"
                        AND o."Status" NOT IN ({processedParam}, {deadLetteredParam})
                  )
                FOR UPDATE SKIP LOCKED
                LIMIT {batchParam}
            )
            """);
    }
}
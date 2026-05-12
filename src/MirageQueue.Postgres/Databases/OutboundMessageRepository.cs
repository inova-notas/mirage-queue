using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using Npgsql;

namespace MirageQueue.Postgres.Databases;

public class OutboundMessageRepository : BaseRepository<MirageQueueDbContext, OutboundMessage>, IOutboundMessageRepository
{
    readonly MirageQueueDbContext _dbContext;
    readonly NpgsqlParameter _statusParam = new NpgsqlParameter("statusParam", (int)OutboundMessageStatus.New);

    public OutboundMessageRepository(MirageQueueDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<OutboundMessage>> GetQueuedMessages(IDbContextTransaction? transaction = null, int limit = 10)
    {

        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var nowParam = new NpgsqlParameter("nowParam", DateTime.UtcNow);

        return await _dbContext.Set<OutboundMessage>()
            .FromSql($"""
                SELECT * FROM mirage_queue."OutboundMessage"
                WHERE "Status" = {_statusParam}
                  AND ("NextRetryAt" IS NULL OR "NextRetryAt" <= {nowParam})
                FOR UPDATE SKIP LOCKED
                LIMIT {limit}
                """)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task MarkProcessing(Guid id, IDbContextTransaction? transaction = null)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var idParam = new NpgsqlParameter("idParam", id);
        var statusParam = new NpgsqlParameter("statusParam", (int)OutboundMessageStatus.Processing);
        var updatedParam = new NpgsqlParameter("updatedParam", DateTime.UtcNow);

        await _dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE mirage_queue."OutboundMessage"
            SET "Status" = {statusParam}, "UpdateAt" = {updatedParam}, "ProcessingStartedAt" = {updatedParam}
            WHERE "Id" = {idParam}
            """);
    }

    public async Task MarkForRetry(Guid id, int attemptCount, DateTime nextRetryAt, string? errorMessage, string? stackTrace, string? exceptionType, IDbContextTransaction? transaction = null)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var idParam = new NpgsqlParameter("idParam", id);
        var statusParam = new NpgsqlParameter("statusParam", (int)OutboundMessageStatus.New);
        var updatedParam = new NpgsqlParameter("updatedParam", DateTime.UtcNow);
        var attemptParam = new NpgsqlParameter("attemptParam", attemptCount);
        var nextRetryParam = new NpgsqlParameter("nextRetryParam", nextRetryAt);
        var errorMessageParam = new NpgsqlParameter("errorMessageParam", (object?)errorMessage ?? DBNull.Value);
        var stackTraceParam = new NpgsqlParameter("stackTraceParam", (object?)stackTrace ?? DBNull.Value);
        var exceptionTypeParam = new NpgsqlParameter("exceptionTypeParam", (object?)exceptionType ?? DBNull.Value);

        await _dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE mirage_queue."OutboundMessage"
            SET "Status" = {statusParam},
                "UpdateAt" = {updatedParam},
                "AttemptCount" = {attemptParam},
                "NextRetryAt" = {nextRetryParam},
                "ProcessingStartedAt" = NULL,
                "ErrorMessage" = {errorMessageParam},
                "StackTrace" = {stackTraceParam},
                "ExceptionType" = {exceptionTypeParam}
            WHERE "Id" = {idParam}
            """);
    }

    public async Task MarkDeadLettered(Guid id, string? errorMessage, string? stackTrace, string? exceptionType, IDbContextTransaction? transaction = null)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var idParam = new NpgsqlParameter("idParam", id);
        var statusParam = new NpgsqlParameter("statusParam", (int)OutboundMessageStatus.DeadLettered);
        var updatedParam = new NpgsqlParameter("updatedParam", DateTime.UtcNow);
        var errorMessageParam = new NpgsqlParameter("errorMessageParam", (object?)errorMessage ?? DBNull.Value);
        var stackTraceParam = new NpgsqlParameter("stackTraceParam", (object?)stackTrace ?? DBNull.Value);
        var exceptionTypeParam = new NpgsqlParameter("exceptionTypeParam", (object?)exceptionType ?? DBNull.Value);

        await _dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE mirage_queue."OutboundMessage"
            SET "Status" = {statusParam},
                "UpdateAt" = {updatedParam},
                "ProcessingStartedAt" = NULL,
                "ErrorMessage" = {errorMessageParam},
                "StackTrace" = {stackTraceParam},
                "ExceptionType" = {exceptionTypeParam}
            WHERE "Id" = {idParam}
            """);
    }

    public async Task<List<OutboundMessage>> GetStuckProcessingMessages(TimeSpan leaseDuration, int limit, IDbContextTransaction? transaction = null)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var processingParam = new NpgsqlParameter("processingParam", (int)OutboundMessageStatus.Processing);
        var cutoffParam = new NpgsqlParameter("cutoffParam", DateTime.UtcNow - leaseDuration);

        return await _dbContext.Set<OutboundMessage>()
            .FromSql($"""
                SELECT * FROM mirage_queue."OutboundMessage"
                WHERE "Status" = {processingParam}
                  AND "ProcessingStartedAt" IS NOT NULL
                  AND "ProcessingStartedAt" < {cutoffParam}
                FOR UPDATE SKIP LOCKED
                LIMIT {limit}
                """)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task ReplayFromDeadLetter(Guid id, IDbContextTransaction? transaction = null)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var idParam = new NpgsqlParameter("idParam", id);
        var statusParam = new NpgsqlParameter("statusParam", (int)OutboundMessageStatus.New);
        var deadLetteredParam = new NpgsqlParameter("deadLetteredParam", (int)OutboundMessageStatus.DeadLettered);
        var updatedParam = new NpgsqlParameter("updatedParam", DateTime.UtcNow);

        await _dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE mirage_queue."OutboundMessage"
            SET "Status" = {statusParam},
                "UpdateAt" = {updatedParam},
                "AttemptCount" = 0,
                "NextRetryAt" = NULL,
                "ProcessingStartedAt" = NULL,
                "ErrorMessage" = NULL,
                "StackTrace" = NULL,
                "ExceptionType" = NULL
            WHERE "Id" = {idParam} AND "Status" = {deadLetteredParam}
            """);
    }
    
    public async Task UpdateMessageStatus(Guid id, OutboundMessageStatus status, IDbContextTransaction? transaction = default)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var idParam = new NpgsqlParameter("idParam", id);
        var statusUpdateParam = new NpgsqlParameter("statusUpdateParam", (int)status);
        var updatedParam = new NpgsqlParameter("updatedParam", DateTime.UtcNow);

        await _dbContext.Database.ExecuteSqlAsync($"UPDATE mirage_queue.\"OutboundMessage\" SET \"Status\" = {statusUpdateParam}, \"UpdateAt\" = {updatedParam} WHERE \"Id\" = {idParam}");
    }

    public async Task UpdateMessageStatus(Guid id, OutboundMessageStatus status, string? errorMessage, string? stackTrace, string? exceptionType, IDbContextTransaction? transaction = default)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var idParam = new NpgsqlParameter("idParam", id);
        var statusUpdateParam = new NpgsqlParameter("statusUpdateParam", (int)status);
        var updatedParam = new NpgsqlParameter("updatedParam", DateTime.UtcNow);
        var errorMessageParam = new NpgsqlParameter("errorMessageParam", (object?)errorMessage ?? DBNull.Value);
        var stackTraceParam = new NpgsqlParameter("stackTraceParam", (object?)stackTrace ?? DBNull.Value);
        var exceptionTypeParam = new NpgsqlParameter("exceptionTypeParam", (object?)exceptionType ?? DBNull.Value);

        await _dbContext.Database.ExecuteSqlAsync($"UPDATE mirage_queue.\"OutboundMessage\" SET \"Status\" = {statusUpdateParam}, \"UpdateAt\" = {updatedParam}, \"ProcessingStartedAt\" = NULL, \"ErrorMessage\" = {errorMessageParam}, \"StackTrace\" = {stackTraceParam}, \"ExceptionType\" = {exceptionTypeParam} WHERE \"Id\" = {idParam}");
    }

    public async Task<bool> InsertIfNotExists(OutboundMessage message, IDbContextTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);

        var idParam = new NpgsqlParameter("id", message.Id);
        var statusParam = new NpgsqlParameter("status", (int)message.Status);
        var consumerEndpointParam = new NpgsqlParameter("consumerEndpoint", message.ConsumerEndpoint);
        var inboundMessageIdParam = new NpgsqlParameter("inboundMessageId", message.InboundMessageId);
        var contentParam = new NpgsqlParameter("content", message.Content);
        var contractParam = new NpgsqlParameter("contract", message.MessageContract);
        var createAtParam = new NpgsqlParameter("createAt", message.CreateAt);
        var updateAtParam = new NpgsqlParameter("updateAt", (object?)message.UpdateAt ?? DBNull.Value);

        var rowsAffected = await _dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO mirage_queue."OutboundMessage"
                ("Id", "Status", "ConsumerEndpoint", "InboundMessageId", "Content", "MessageContract", "CreateAt", "UpdateAt")
            VALUES
                ({idParam}, {statusParam}, {consumerEndpointParam}, {inboundMessageIdParam}, {contentParam}::jsonb, {contractParam}, {createAtParam}, {updateAtParam})
            ON CONFLICT ("InboundMessageId", "ConsumerEndpoint") DO NOTHING
            """,
            cancellationToken);

        return rowsAffected > 0;
    }

    public async Task<int> DeleteTerminalOlderThan(DateTime cutoff, int batchSize, IDbContextTransaction? transaction = null)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var processedParam = new NpgsqlParameter("processedParam", (int)OutboundMessageStatus.Processed);
        var deadLetteredParam = new NpgsqlParameter("deadLetteredParam", (int)OutboundMessageStatus.DeadLettered);
        var cutoffParam = new NpgsqlParameter("cutoffParam", cutoff);
        var batchParam = new NpgsqlParameter("batchParam", batchSize);

        return await _dbContext.Database.ExecuteSqlAsync(
            $"""
            DELETE FROM mirage_queue."OutboundMessage"
            WHERE "Id" IN (
                SELECT "Id" FROM mirage_queue."OutboundMessage"
                WHERE "Status" IN ({processedParam}, {deadLetteredParam})
                  AND COALESCE("UpdateAt", "CreateAt") < {cutoffParam}
                FOR UPDATE SKIP LOCKED
                LIMIT {batchParam}
            )
            """);
    }
}
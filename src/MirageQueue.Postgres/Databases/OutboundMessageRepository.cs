using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using Npgsql;
using NpgsqlTypes;

namespace MirageQueue.Postgres.Databases;

public class OutboundMessageRepository : BaseRepository<MirageQueueDbContext, OutboundMessage>, IOutboundMessageRepository
{
    readonly MirageQueueDbContext _dbContext;

    public OutboundMessageRepository(MirageQueueDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<OutboundMessage>> ClaimQueuedMessages(IReadOnlyList<Guid> processingTokens, IDbContextTransaction? transaction = null)
    {
        if (processingTokens.Count == 0)
            return [];

        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var tokensParam = new NpgsqlParameter("tokens", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = processingTokens.ToArray()
        };
        var limitParam = new NpgsqlParameter("limit", processingTokens.Count);
        var newStatusParam = new NpgsqlParameter("newStatus", (int)OutboundMessageStatus.New);
        var processingStatusParam = new NpgsqlParameter("processingStatus", (int)OutboundMessageStatus.Processing);
        var updatedParam = new NpgsqlParameter("updated", DateTime.UtcNow);

        return await _dbContext.Set<OutboundMessage>()
            .FromSqlRaw(
                @"
WITH token_input AS (
    SELECT token, ordinality
    FROM unnest(@tokens) WITH ORDINALITY AS t(token, ordinality)
),
locked_rows AS (
    SELECT o.""Id"", o.""CreateAt"", ROW_NUMBER() OVER (ORDER BY o.""CreateAt"", o.""Id"") AS row_number
    FROM mirage_queue.""OutboundMessage"" o
    WHERE o.""Status"" = @newStatus
    ORDER BY o.""CreateAt"", o.""Id""
    FOR UPDATE SKIP LOCKED
    LIMIT @limit
),
claimed AS (
    SELECT l.""Id"", t.token AS ""ProcessingToken""
    FROM locked_rows l
    JOIN token_input t ON t.ordinality = l.row_number
)
UPDATE mirage_queue.""OutboundMessage"" o
SET
    ""Status"" = @processingStatus,
    ""ProcessingToken"" = c.""ProcessingToken"",
    ""UpdateAt"" = @updated
FROM claimed c
WHERE o.""Id"" = c.""Id""
RETURNING o.*",
                tokensParam,
                limitParam,
                newStatusParam,
                processingStatusParam,
                updatedParam)
            .AsNoTracking()
            .ToListAsync();
    }
    
    public async Task UpdateMessageStatus(Guid id, OutboundMessageStatus status, IDbContextTransaction? transaction = default)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var idParam = new NpgsqlParameter("idParam", id);
        var statusUpdateParam = new NpgsqlParameter("statusUpdateParam", (int)status);
        var updatedParam = new NpgsqlParameter("updatedParam", DateTime.UtcNow);

        await _dbContext.Database.ExecuteSqlAsync($"UPDATE mirage_queue.\"OutboundMessage\" SET \"Status\" = {statusUpdateParam}, \"UpdateAt\" = {updatedParam}, \"ProcessingToken\" = null WHERE \"Id\" = {idParam}");
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

        await _dbContext.Database.ExecuteSqlAsync($"UPDATE mirage_queue.\"OutboundMessage\" SET \"Status\" = {statusUpdateParam}, \"UpdateAt\" = {updatedParam}, \"ErrorMessage\" = {errorMessageParam}, \"StackTrace\" = {stackTraceParam}, \"ExceptionType\" = {exceptionTypeParam}, \"ProcessingToken\" = null WHERE \"Id\" = {idParam}");
    }

    public async Task<bool> TryUpdateMessageStatus(Guid id, Guid processingToken, OutboundMessageStatus status)
    {
        var idParam = new NpgsqlParameter("idParam", id);
        var tokenParam = new NpgsqlParameter("tokenParam", processingToken);
        var expectedStatusParam = new NpgsqlParameter("expectedStatusParam", (int)OutboundMessageStatus.Processing);
        var statusUpdateParam = new NpgsqlParameter("statusUpdateParam", (int)status);
        var updatedParam = new NpgsqlParameter("updatedParam", DateTime.UtcNow);

        var affected = await _dbContext.Database.ExecuteSqlAsync($"UPDATE mirage_queue.\"OutboundMessage\" SET \"Status\" = {statusUpdateParam}, \"UpdateAt\" = {updatedParam}, \"ProcessingToken\" = null, \"ErrorMessage\" = null, \"StackTrace\" = null, \"ExceptionType\" = null WHERE \"Id\" = {idParam} AND \"Status\" = {expectedStatusParam} AND \"ProcessingToken\" = {tokenParam}");
        return affected == 1;
    }

    public async Task<bool> TryUpdateMessageStatus(Guid id, Guid processingToken, OutboundMessageStatus status, string? errorMessage, string? stackTrace, string? exceptionType)
    {
        var idParam = new NpgsqlParameter("idParam", id);
        var tokenParam = new NpgsqlParameter("tokenParam", processingToken);
        var expectedStatusParam = new NpgsqlParameter("expectedStatusParam", (int)OutboundMessageStatus.Processing);
        var statusUpdateParam = new NpgsqlParameter("statusUpdateParam", (int)status);
        var updatedParam = new NpgsqlParameter("updatedParam", DateTime.UtcNow);
        var errorMessageParam = new NpgsqlParameter("errorMessageParam", (object?)errorMessage ?? DBNull.Value);
        var stackTraceParam = new NpgsqlParameter("stackTraceParam", (object?)stackTrace ?? DBNull.Value);
        var exceptionTypeParam = new NpgsqlParameter("exceptionTypeParam", (object?)exceptionType ?? DBNull.Value);

        var affected = await _dbContext.Database.ExecuteSqlAsync($"UPDATE mirage_queue.\"OutboundMessage\" SET \"Status\" = {statusUpdateParam}, \"UpdateAt\" = {updatedParam}, \"ErrorMessage\" = {errorMessageParam}, \"StackTrace\" = {stackTraceParam}, \"ExceptionType\" = {exceptionTypeParam}, \"ProcessingToken\" = null WHERE \"Id\" = {idParam} AND \"Status\" = {expectedStatusParam} AND \"ProcessingToken\" = {tokenParam}");
        return affected == 1;
    }

    public async Task<int> ResetStuckProcessingMessages(int timeoutInMinutes, IDbContextTransaction? transaction = default)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var newStatus = new NpgsqlParameter("newStatus", (int)OutboundMessageStatus.New);
        var processingStatus = new NpgsqlParameter("processingStatus", (int)OutboundMessageStatus.Processing);
        var cutoff = new NpgsqlParameter("cutoff", DateTime.UtcNow.AddMinutes(-timeoutInMinutes));
        var updated = new NpgsqlParameter("updated", DateTime.UtcNow);

        return await _dbContext.Database.ExecuteSqlAsync($"UPDATE mirage_queue.\"OutboundMessage\" SET \"Status\" = {newStatus}, \"UpdateAt\" = {updated}, \"ProcessingToken\" = null WHERE \"Status\" = {processingStatus} AND \"UpdateAt\" < {cutoff}");
    }

    public async Task<bool> TryUpdateHeartbeat(Guid messageId, Guid processingToken)
    {
        var idParam = new NpgsqlParameter("idParam", messageId);
        var tokenParam = new NpgsqlParameter("tokenParam", processingToken);
        var expectedStatusParam = new NpgsqlParameter("expectedStatusParam", (int)OutboundMessageStatus.Processing);
        var updatedParam = new NpgsqlParameter("updatedParam", DateTime.UtcNow);

        var affected = await _dbContext.Database.ExecuteSqlAsync($"UPDATE mirage_queue.\"OutboundMessage\" SET \"UpdateAt\" = {updatedParam} WHERE \"Id\" = {idParam} AND \"Status\" = {expectedStatusParam} AND \"ProcessingToken\" = {tokenParam}");
        return affected == 1;
    }
}

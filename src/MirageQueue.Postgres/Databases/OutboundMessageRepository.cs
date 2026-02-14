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

        return await _dbContext.Set<OutboundMessage>()
            .FromSql($"SELECT * FROM mirage_queue.\"OutboundMessage\" WHERE \"Status\" = {_statusParam} FOR UPDATE SKIP LOCKED LIMIT {limit}")
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

        await _dbContext.Database.ExecuteSqlAsync($"UPDATE mirage_queue.\"OutboundMessage\" SET \"Status\" = {statusUpdateParam}, \"UpdateAt\" = {updatedParam}, \"ErrorMessage\" = {errorMessageParam}, \"StackTrace\" = {stackTraceParam}, \"ExceptionType\" = {exceptionTypeParam} WHERE \"Id\" = {idParam}");
    }
}
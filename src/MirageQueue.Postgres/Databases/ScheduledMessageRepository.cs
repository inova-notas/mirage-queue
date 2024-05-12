using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using Npgsql;

namespace MirageQueue.Postgres.Databases;

public class ScheduledMessageRepository : BaseRepository<MirageQueueDbContext, ScheduledInboundMessage>, IScheduledMessageRepository
{
    private readonly MirageQueueDbContext _dbContext;
    private NpgsqlParameter statusParam;

    public ScheduledMessageRepository(MirageQueueDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
        statusParam = new NpgsqlParameter("statusParam", DbType.Int32)
        {
            Value = (int)ScheduledInboundMessageStatus.WaitingScheduledTime
        };
    }

    public async Task<List<ScheduledInboundMessage>> GetScheduledMessages(int limit, IDbContextTransaction? transaction = default)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());
        
        var limitParam = new NpgsqlParameter("limitParam", limit);
        var nowParam = new NpgsqlParameter("nowParam", DateTime.UtcNow);

        return await _dbContext.Set<ScheduledInboundMessage>()
            .FromSql($"SELECT * FROM mirage_queue.\"ScheduledInboundMessage\" WHERE \"Status\" = {statusParam} AND \"ExecuteAt\" <= {nowParam} FOR UPDATE SKIP LOCKED LIMIT {limitParam}")
            .AsNoTracking()
            .ToListAsync();
    }
    
    public async Task UpdateMessageStatus(Guid id, ScheduledInboundMessageStatus status, IDbContextTransaction? transaction = default)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var idParam = new NpgsqlParameter("idParam", id);
        var statusUpdateParam = new NpgsqlParameter("statusUpdateParam", (int)status);
        var updatedParam = new NpgsqlParameter("updatedParam", DateTime.UtcNow);

        await _dbContext.Database.ExecuteSqlAsync($"UPDATE mirage_queue.\"ScheduledInboundMessage\" SET \"Status\" = {statusUpdateParam}, \"UpdateAt\" = {updatedParam} WHERE \"Id\" = {idParam}");
    }
}
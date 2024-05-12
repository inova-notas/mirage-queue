using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using Npgsql;

namespace MirageQueue.Postgres.Databases;

public class InboundMessageRepository : BaseRepository<MirageQueueDbContext, InboundMessage>, IInboundMessageRepository
{
    private readonly MirageQueueDbContext _dbContext;
    private NpgsqlParameter statusParam;
    
    public InboundMessageRepository(MirageQueueDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
        statusParam = new NpgsqlParameter("statusParam", DbType.Int32)
        {
            Value = (int)InboundMessageStatus.New
        };
    }

    public async Task<List<InboundMessage>> GetQueuedMessages(int limit, IDbContextTransaction? transaction = default)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        // var sql =
        //     $"SELECT * FROM mirage_queue.\"{nameof(InboundMessage)}\" WHERE \"{nameof(InboundMessage.Status)}\" = {(int)InboundMessageStatus.New} FOR UPDATE SKIP LOCKED LIMIT {limit}";

        var limitParam = new NpgsqlParameter("limitParam", limit);
        
        return await _dbContext.Set<InboundMessage>()
            .FromSql($"SELECT * FROM mirage_queue.\"InboundMessage\" WHERE \"Status\" = {statusParam} FOR UPDATE SKIP LOCKED LIMIT {limitParam}")
            .ToListAsync();
    }
}
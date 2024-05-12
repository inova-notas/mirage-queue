using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using Npgsql;

namespace MirageQueue.Postgres.Databases;

public class OutboundMessageRepository : BaseRepository<MirageQueueDbContext, OutboundMessage>, IOutboundMessageRepository
{
    private readonly MirageQueueDbContext _dbContext;
    private NpgsqlParameter statusParam = new NpgsqlParameter("statusParam", (int)OutboundMessageStatus.New);

    public OutboundMessageRepository(MirageQueueDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<OutboundMessage>> GetQueuedMessages(int limit, IDbContextTransaction? transaction = default)
    {

        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        // var sql =
        //     $"SELECT * FROM mirage_queue.\"{nameof(OutboundMessage)}\" WHERE \"{nameof(OutboundMessage.Status)}\" = {(int)OutboundMessageStatus.New} FOR UPDATE SKIP LOCKED LIMIT {limit}";

        var limitParam = new NpgsqlParameter("limitParam", limit);

        return await _dbContext.Set<OutboundMessage>()
            .FromSql($"SELECT * FROM mirage_queue.\"OutboundMessage\" WHERE \"Status\" = {statusParam} FOR UPDATE SKIP LOCKED LIMIT {limitParam}")
            .ToListAsync();
    }
}
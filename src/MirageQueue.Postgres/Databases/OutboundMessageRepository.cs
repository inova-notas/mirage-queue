using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;

namespace MirageQueue.Postgres.Databases;

public class OutboundMessageRepository : BaseRepository<MirageQueueDbContext, OutboundMessage>, IOutboundMessageRepository
{
    private readonly MirageQueueDbContext _dbContext;

    public OutboundMessageRepository(MirageQueueDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<OutboundMessage>> GetQueuedMessages(int limit, IDbContextTransaction? transaction = default)
    {

        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var sql =
            $"SELECT * FROM \"{nameof(OutboundMessage)}\" WHERE \"{nameof(OutboundMessage.Status)}\" = {(int)OutboundMessageStatus.New} FOR UPDATE SKIP LOCKED LIMIT {limit}";

        return await _dbContext.Set<OutboundMessage>()
            .FromSqlRaw(sql)
            .ToListAsync();
    }
}
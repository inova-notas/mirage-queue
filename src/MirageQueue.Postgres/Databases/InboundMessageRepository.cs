using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;

namespace MirageQueue.Postgres.Databases;

public class InboundMessageRepository : BaseRepository<MirageQueueDbContext, InboundMessage>, IInboundMessageRepository
{
    private readonly MirageQueueDbContext _dbContext;

    public InboundMessageRepository(MirageQueueDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<InboundMessage>> GetQueuedMessages(int limit, IDbContextTransaction? transaction = default)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var sql =
            $"SELECT * FROM \"{nameof(InboundMessage)}\" WHERE \"{nameof(InboundMessage.Status)}\" = {(int)InboundMessageStatus.New} FOR UPDATE SKIP LOCKED LIMIT {limit}";

        return await _dbContext.Set<InboundMessage>()
            .FromSqlRaw(sql)
            .ToListAsync();
    }
}
using MirageQueue.Databases;

namespace MirageQueue.Messages.Services;

internal class OutboundMessageService
{
    private readonly MirageQueueDbContext _dbContext;

    public OutboundMessageService(MirageQueueDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}
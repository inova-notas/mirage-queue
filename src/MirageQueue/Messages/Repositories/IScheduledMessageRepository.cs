using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Messages.Repositories;

public interface IScheduledMessageRepository : IRepository<ScheduledInboundMessage>
{
    public Task<List<ScheduledInboundMessage>> GetScheduledMessages(int limit, IDbContextTransaction? transaction = default);
}
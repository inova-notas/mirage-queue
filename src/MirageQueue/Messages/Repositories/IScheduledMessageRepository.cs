using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Messages.Repositories;

public interface IScheduledMessageRepository : IRepository<ScheduledInboundMessage>
{
    public Task<List<ScheduledInboundMessage>> GetScheduledMessages(IDbContextTransaction? transaction = null, int limit = 10);
    public Task UpdateMessageStatus(Guid id, ScheduledInboundMessageStatus status, IDbContextTransaction? transaction = null);
}
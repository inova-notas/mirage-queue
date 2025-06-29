using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Messages.Repositories;

public interface IOutboundMessageRepository : IRepository<OutboundMessage>
{
    public Task<List<OutboundMessage>> GetQueuedMessages(IDbContextTransaction? transaction = null, int limit = 10);
    public Task UpdateMessageStatus(Guid id, OutboundMessageStatus status, IDbContextTransaction? transaction = null);
}
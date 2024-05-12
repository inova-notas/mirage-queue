using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Messages.Repositories;

public interface IOutboundMessageRepository : IRepository<OutboundMessage>
{
    public Task<List<OutboundMessage>> GetQueuedMessages(int limit, IDbContextTransaction? transaction = default);
    public Task UpdateMessageStatus(Guid id, OutboundMessageStatus status, IDbContextTransaction? transaction = default);
}
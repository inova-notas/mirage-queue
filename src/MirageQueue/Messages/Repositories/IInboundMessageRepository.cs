using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;
using MirageQueue.Publishers.Abstractions;

namespace MirageQueue.Messages.Repositories;

public interface IInboundMessageRepository : IRepository<InboundMessage>
{
    public Task<List<InboundMessage>> GetQueuedMessages(IDbContextTransaction? transaction = null, int limit = 10);
    public Task UpdateMessageStatus(Guid id, InboundMessageStatus status, IDbContextTransaction? transaction = null);
    public Task InsertDirect(InboundMessage message, DbTransaction transaction, CancellationToken cancellationToken = default);
    public Task<PublishResult> InsertIfNotExists(InboundMessage message, CancellationToken cancellationToken = default);
    public Task<PublishResult> InsertDirectIfNotExists(InboundMessage message, DbTransaction transaction, CancellationToken cancellationToken = default);
}
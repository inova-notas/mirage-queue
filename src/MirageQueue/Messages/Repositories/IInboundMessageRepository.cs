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

    /// <summary>
    /// Bulk-delete inbound rows whose effective timestamp is older than <paramref name="cutoff"/>
    /// AND whose status is <see cref="InboundMessageStatus.Queued"/> (post-fan-out terminal) AND
    /// have NO outbound child in a non-terminal state. This last predicate guards against the
    /// FK cascade silently destroying active queue work. Returns rows actually deleted.
    /// </summary>
    public Task<int> DeleteQueuedOlderThanWithNoActiveOutbound(DateTime cutoff, int batchSize, IDbContextTransaction? transaction = null);
}
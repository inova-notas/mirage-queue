using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Messages.Repositories;

public interface IOutboundMessageRepository : IRepository<OutboundMessage>
{
    public Task<List<OutboundMessage>> GetQueuedMessages(IDbContextTransaction? transaction = null, int limit = 10);
    public Task UpdateMessageStatus(Guid id, OutboundMessageStatus status, IDbContextTransaction? transaction = null);
    public Task UpdateMessageStatus(Guid id, OutboundMessageStatus status, string? errorMessage, string? stackTrace, string? exceptionType, IDbContextTransaction? transaction = null);

    /// <summary>
    /// Inserts a fan-out outbound row, ignoring conflicts on the (InboundMessageId, ConsumerEndpoint) unique index.
    /// Returns true if a new row was inserted; false if a duplicate already existed.
    /// </summary>
    public Task<bool> InsertIfNotExists(OutboundMessage message, IDbContextTransaction? transaction = null, CancellationToken cancellationToken = default);
}
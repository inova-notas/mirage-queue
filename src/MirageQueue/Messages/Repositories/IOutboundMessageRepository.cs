using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Messages.Repositories;

public interface IOutboundMessageRepository : IRepository<OutboundMessage>
{
    public Task<List<OutboundMessage>> ClaimQueuedMessages(IReadOnlyList<Guid> processingTokens, IDbContextTransaction? transaction = null);
    public Task UpdateMessageStatus(Guid id, OutboundMessageStatus status, IDbContextTransaction? transaction = null);
    public Task UpdateMessageStatus(Guid id, OutboundMessageStatus status, string? errorMessage, string? stackTrace, string? exceptionType, IDbContextTransaction? transaction = null);
    public Task<bool> TryUpdateMessageStatus(Guid id, Guid processingToken, OutboundMessageStatus status);
    public Task<bool> TryUpdateMessageStatus(Guid id, Guid processingToken, OutboundMessageStatus status, string? errorMessage, string? stackTrace, string? exceptionType);
    public Task<int> ResetStuckProcessingMessages(int timeoutInMinutes, IDbContextTransaction? transaction = null);
    public Task<bool> TryUpdateHeartbeat(Guid messageId, Guid processingToken);
}

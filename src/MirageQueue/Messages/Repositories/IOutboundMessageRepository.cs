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
    /// Transition a picked-up row to <c>Processing</c> and stamp <c>ProcessingStartedAt = now()</c>
    /// for stuck-row recovery.
    /// </summary>
    public Task MarkProcessing(Guid id, IDbContextTransaction? transaction = null);

    /// <summary>
    /// Reset the row to <c>Status = New</c> with an incremented <c>AttemptCount</c> and a future
    /// <c>NextRetryAt</c>. Used after a failed dispatch when the policy still has retries left.
    /// </summary>
    public Task MarkForRetry(Guid id, int attemptCount, DateTime nextRetryAt, string? errorMessage, string? stackTrace, string? exceptionType, IDbContextTransaction? transaction = null);

    /// <summary>
    /// Terminal transition for rows whose policy attempts are exhausted.
    /// </summary>
    public Task MarkDeadLettered(Guid id, string? errorMessage, string? stackTrace, string? exceptionType, IDbContextTransaction? transaction = null);

    /// <summary>
    /// Picks up rows that have been in <c>Processing</c> for longer than the lease duration.
    /// Used by the stuck-Processing reaper.
    /// </summary>
    public Task<List<OutboundMessage>> GetStuckProcessingMessages(TimeSpan leaseDuration, int limit, IDbContextTransaction? transaction = null);

    /// <summary>
    /// Replay a dead-lettered row: reset to <c>Status = New</c>, <c>AttemptCount = 0</c>,
    /// <c>NextRetryAt = null</c>, <c>ProcessingStartedAt = null</c>.
    /// </summary>
    public Task ReplayFromDeadLetter(Guid id, IDbContextTransaction? transaction = null);

    /// <summary>
    /// Inserts a fan-out outbound row, ignoring conflicts on the (InboundMessageId, ConsumerEndpoint) unique index.
    /// Returns true if a new row was inserted; false if a duplicate already existed.
    /// </summary>
    public Task<bool> InsertIfNotExists(OutboundMessage message, IDbContextTransaction? transaction = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-delete outbound rows in terminal status (<see cref="OutboundMessageStatus.Processed"/> or
    /// <see cref="OutboundMessageStatus.DeadLettered"/>) whose effective timestamp
    /// (<c>COALESCE(UpdateAt, CreateAt)</c>) is older than <paramref name="cutoff"/>. Bounded by
    /// <paramref name="batchSize"/>. Returns the number of rows actually deleted.
    /// </summary>
    public Task<int> DeleteTerminalOlderThan(DateTime cutoff, int batchSize, IDbContextTransaction? transaction = null);
}
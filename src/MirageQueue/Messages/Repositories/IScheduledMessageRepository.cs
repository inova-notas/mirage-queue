using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;
using MirageQueue.Publishers.Abstractions;

namespace MirageQueue.Messages.Repositories;

public interface IScheduledMessageRepository : IRepository<ScheduledInboundMessage>
{
    public Task<List<ScheduledInboundMessage>> GetScheduledMessages(IDbContextTransaction? transaction = null, int limit = 10);
    public Task UpdateMessageStatus(Guid id, ScheduledInboundMessageStatus status, IDbContextTransaction? transaction = null);
    public Task InsertDirect(ScheduledInboundMessage message, DbTransaction transaction, CancellationToken cancellationToken = default);
    public Task<PublishResult> InsertIfNotExists(ScheduledInboundMessage message, CancellationToken cancellationToken = default);
    public Task<PublishResult> InsertDirectIfNotExists(ScheduledInboundMessage message, DbTransaction transaction, CancellationToken cancellationToken = default);
}
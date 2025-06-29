using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Consumers.Abstractions;

public interface IMessageHandler
{
    Task HandleQueuedInboundMessages(IDbContextTransaction dbTransaction);
    Task<List<OutboundMessage>> HandleQueuedOutboundMessages(IDbContextTransaction dbTransaction);
    Task AddOutboundMessageToChannel(OutboundMessage message);
    Task<(Guid MessageId, OutboundMessageStatus Status, Exception? Exception)> ProcessOutboundMessage(OutboundMessage message);
    Task HandleScheduledMessages(IDbContextTransaction dbTransaction);
}
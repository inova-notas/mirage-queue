using Microsoft.EntityFrameworkCore.Storage;

namespace MirageQueue.Consumers.Abstractions;

public interface IMessageHandler
{
    Task HandleQueuedInboundMessages(IDbContextTransaction dbTransaction);
    Task HandleQueuedOutboundMessages(IDbContextTransaction dbTransaction);
}
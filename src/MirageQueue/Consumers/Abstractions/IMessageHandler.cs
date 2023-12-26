namespace MirageQueue.Consumers.Abstractions;

public interface IMessageHandler
{
    Task HandleQueuedInboundMessages();
    Task HandleQueuedOutboundMessages();
}
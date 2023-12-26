namespace MirageQueue.Messages.Entities;

internal class InboundMessage : BaseMessage
{
    public required InboundMessageStatus Status { get; set; }
    
}
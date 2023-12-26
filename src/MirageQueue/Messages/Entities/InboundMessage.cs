namespace MirageQueue.Messages.Entities;

public class InboundMessage : BaseMessage
{
    public required InboundMessageStatus Status { get; set; }
}
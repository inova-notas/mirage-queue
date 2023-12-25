namespace MirageQueue.Messages.Entities;

internal class InboundMessage
{
    public required InboundMessageStatus Status { get; set; }
    public required string MessageContract { get; set; }
}
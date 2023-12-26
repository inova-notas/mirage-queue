namespace MirageQueue.Messages.Entities;

internal class OutboundMessage : BaseMessage
{
    public required OutBoundMessageStatus Status { get; set; }
    public required string ConsumerEndpoint { get; set; }
}
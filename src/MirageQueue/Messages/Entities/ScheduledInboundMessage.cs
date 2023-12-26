namespace MirageQueue.Messages.Entities;

internal class ScheduledInboundMessage : BaseMessage
{
    public required ScheduledInboundMessageStatus Status { get; set; }
    public required DateTime ExecuteAt { get; set; }
}
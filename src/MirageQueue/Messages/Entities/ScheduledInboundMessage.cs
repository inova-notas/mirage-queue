namespace MirageQueue.Messages.Entities;

public class ScheduledInboundMessage : BaseMessage
{
    public required ScheduledInboundMessageStatus Status { get; set; }
    public required DateTime ExecuteAt { get; set; }
}
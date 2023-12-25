namespace Mirage.Messages;

public class ScheduledInBoundMessage : InBoundMessage
{
    public DateTime ExecuteAt { get; set; }
}
namespace Mirage.Messages;

public class InBoundMessage : BaseMessage
{
    public required InBoundMessageStatus Status { get; set; }
    public required string MessageContract { get; set; }
}
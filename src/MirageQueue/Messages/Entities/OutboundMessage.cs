namespace MirageQueue.Messages.Entities;

public class OutboundMessage : BaseMessage
{
    public required OutboundMessageStatus Status { get; set; }
    public required string ConsumerEndpoint { get; set; }
    public required Guid InboundMessageId { get; set; }
    public InboundMessage? InboundMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public string? ExceptionType { get; set; }

    public void ChangeStatus(OutboundMessageStatus status)
    {
        Status = status;
        UpdateAt = DateTime.UtcNow;
    }
}
namespace Mirage.Messages;

public class OutBoundMessage
{
    public required OutBoundMessageStatus Status { get; set; }
    public required string ConsumerEndpoint { get; set; }
}
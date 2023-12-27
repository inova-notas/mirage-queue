namespace MirageQueue.Consumers;

public class DispatcherConsumer
{
    public required string MessageContract { get; set; }
    public required string ConsumerEndpoint { get; set; }
    public required Type ConsumerType { get; set; }
    public required Type MessageType { get; set; }
}
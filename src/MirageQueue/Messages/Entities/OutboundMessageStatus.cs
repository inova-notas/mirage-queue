namespace MirageQueue.Messages.Entities;

public enum OutboundMessageStatus
{
    Creating = 0,
    New = 1,
    Processing = 2,
    Processed = 3,
    Failed = 4,
    DeadLettered = 5,
}
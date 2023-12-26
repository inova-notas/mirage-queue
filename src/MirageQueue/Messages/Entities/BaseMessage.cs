namespace MirageQueue.Messages.Entities;

public abstract class BaseMessage
{
    public required Guid Id { get; set; }
    public required string Content { get; set; }
    public required string MessageContract { get; set; }
    public required DateTime CreateAt { get; set; }
    public DateTime? UpdateAt { get; set; }
}
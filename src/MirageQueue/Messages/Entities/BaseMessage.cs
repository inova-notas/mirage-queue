namespace MirageQueue.Messages.Entities;

internal abstract class BaseMessage
{
    public required Guid Id { get; set; }
    public required string Content { get; set; }
    public required DateTime CreateAt { get; set; }
    public required DateTime UpdateAt { get; set; }
}
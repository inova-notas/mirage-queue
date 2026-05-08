namespace MirageQueue.Tests.Consumers.Fixtures;

public class NoOpMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

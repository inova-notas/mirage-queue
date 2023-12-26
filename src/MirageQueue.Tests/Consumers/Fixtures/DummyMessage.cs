namespace MirageQueue.Tests.Consumers.Fixtures;

public class DummyMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
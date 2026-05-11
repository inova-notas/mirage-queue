namespace MirageQueue.Tests.Consumers.Fixtures;

public class TransientFailingMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

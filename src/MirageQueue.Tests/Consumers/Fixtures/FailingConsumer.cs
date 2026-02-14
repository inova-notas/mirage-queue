using MirageQueue.Consumers.Abstractions;

namespace MirageQueue.Tests.Consumers.Fixtures;

public class FailingConsumer : IConsumer<DummyMessage>
{
    public Task Process(DummyMessage message)
    {
        throw new InvalidOperationException("Simulated consumer failure");
    }
}

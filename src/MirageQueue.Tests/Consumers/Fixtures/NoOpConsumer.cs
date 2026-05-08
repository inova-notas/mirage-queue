using MirageQueue.Consumers.Abstractions;

namespace MirageQueue.Tests.Consumers.Fixtures;

public class NoOpConsumer : IConsumer<NoOpMessage>
{
    public Task Process(NoOpMessage message) => Task.CompletedTask;
}

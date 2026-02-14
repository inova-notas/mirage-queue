using MirageQueue.Consumers.Abstractions;

namespace ExampleApi;

public class FailingMessageConsumer : IConsumer<FailingMessage>
{
    public Task Process(FailingMessage message)
    {
        throw new InvalidOperationException($"Simulated failure processing message {message.Id}");
    }
}

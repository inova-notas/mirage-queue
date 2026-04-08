using MirageQueue.Consumers.Abstractions;
using Microsoft.Extensions.Logging;

namespace ExampleApi;

public class AnotherTestMessageConsumer(ILogger<AnotherTestMessageConsumer> logger) : IConsumer<TestMessage>
{
    public Task Process(TestMessage message)
    {
        logger.LogDebug("Processing Another TestMessage {MessageId}", message.Id);
        return Task.CompletedTask;
    }
}

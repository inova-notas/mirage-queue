using MirageQueue.Consumers.Abstractions;
using Microsoft.Extensions.Logging;

namespace ExampleApi;

public class TestMessageConsumer(ILogger<TestMessageConsumer> logger) : IConsumer<TestMessage>
{
    private readonly Random _random = new Random();
    public async Task Process(TestMessage message)
    {
        logger.LogDebug("Processing TestMessage {MessageId}", message.Id);
        await Task.Delay(TimeSpan.FromMicroseconds(_random.Next(40, 100)));
    }
}

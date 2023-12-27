using System.Text.Json;
using MirageQueue.Consumers.Abstractions;

namespace ExampleApi;

public class TestMessageConsumer : IConsumer<TestMessage>
{
    public Task Process(TestMessage message)
    {
        Console.WriteLine($"Test message: {JsonSerializer.Serialize(message)}");
        return Task.CompletedTask;
    }
}
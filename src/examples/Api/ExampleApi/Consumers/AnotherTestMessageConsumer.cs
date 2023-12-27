using System.Text.Json;
using MirageQueue.Consumers.Abstractions;

namespace ExampleApi;

public class AnotherTestMessageConsumer : IConsumer<TestMessage>
{
    public Task Process(TestMessage message)
    {
        Console.WriteLine($"Another test message: {JsonSerializer.Serialize(message)}");
        return Task.CompletedTask;
    }
}
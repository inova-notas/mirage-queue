using MirageQueue.Consumers.Abstractions;
using System.Text.Json;

namespace ExampleApi;

public class CreateBusinessConsumer : IConsumer<TestMessage>
{
    public Task Process(TestMessage message)
    {
        Console.WriteLine($"Create Business test message: {JsonSerializer.Serialize(message)}");
        return Task.CompletedTask;
    }
}
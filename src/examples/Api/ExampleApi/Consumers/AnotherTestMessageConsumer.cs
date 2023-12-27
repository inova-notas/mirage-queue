using MirageQueue.Consumers.Abstractions;
using System.Text.Json;

namespace ExampleApi;

public class AnotherTestMessageConsumer : IConsumer<TestMessage>
{
    public Task Process(TestMessage message)
    {
        Console.WriteLine($"Another test message {DateTime.Now:hh:mm:ss} {JsonSerializer.Serialize(message)}");
        return Task.CompletedTask;
    }
}
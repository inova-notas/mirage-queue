using MirageQueue.Consumers.Abstractions;

namespace MirageQueue.IntegrationTests.Fixtures;

public class TraceTestConsumer : IConsumer<TraceTestMessage>
{
    public static Func<TraceTestMessage, Task> Behavior { get; set; } = _ => Task.CompletedTask;

    public Task Process(TraceTestMessage message) => Behavior(message);
}

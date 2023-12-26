using MirageQueue.Consumers.Abstractions;

namespace MirageQueue.Tests.Consumers.Fixtures;

public class DummyConsumer(IDummyService dummyService) : IConsumer<DummyMessage>
{
    public Task Process(DummyMessage message)
    {
        dummyService.DoSomething();
        return Task.CompletedTask;
    }
}
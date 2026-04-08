using MirageQueue.Consumers.Abstractions;

namespace ExampleApi;

public class PressureTestMessageConsumer : IConsumer<PressureTestMessage>
{
    public async Task Process(PressureTestMessage message)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(10000));
    }
}

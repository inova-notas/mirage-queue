using System.Diagnostics;

namespace MirageQueue.Consumers.Abstractions;

public abstract class Consumer<TMessage> : IConsumer
{
    public abstract Task Process(TMessage message);
}
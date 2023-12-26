namespace MirageQueue.Consumers.Abstractions;

public interface IConsumer<in TMessage> : IConsumer
    where TMessage : class
{
    Task Process(TMessage message);
}

public interface IConsumer
{
    
}
namespace MirageQueue.Publishers.Abstractions;

public interface IPublisher
{
    Task Publish<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class;
    
    
    Task Schedule<TMessage>(TMessage message, DateTime scheduledTime, CancellationToken cancellationToken = default)
        where TMessage : class;
}
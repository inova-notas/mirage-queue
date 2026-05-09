using System.Data.Common;

namespace MirageQueue.Publishers.Abstractions;

public interface IPublisher
{
    Task Publish<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class;

    Task Publish<TMessage>(TMessage message, DbTransaction transaction, CancellationToken cancellationToken = default)
        where TMessage : class;

    Task<PublishResult> Publish<TMessage>(TMessage message, string idempotencyKey, CancellationToken cancellationToken = default)
        where TMessage : class;

    Task<PublishResult> Publish<TMessage>(TMessage message, string idempotencyKey, DbTransaction transaction, CancellationToken cancellationToken = default)
        where TMessage : class;

    Task Schedule<TMessage>(TMessage message, DateTime scheduledTime, CancellationToken cancellationToken = default)
        where TMessage : class;

    Task Schedule<TMessage>(TMessage message, DateTime scheduledTime, DbTransaction transaction, CancellationToken cancellationToken = default)
        where TMessage : class;

    Task<PublishResult> Schedule<TMessage>(TMessage message, DateTime scheduledTime, string idempotencyKey, CancellationToken cancellationToken = default)
        where TMessage : class;

    Task<PublishResult> Schedule<TMessage>(TMessage message, DateTime scheduledTime, string idempotencyKey, DbTransaction transaction, CancellationToken cancellationToken = default)
        where TMessage : class;
}
using Microsoft.EntityFrameworkCore;
using MirageQueue.Publishers.Abstractions;

namespace MirageQueue.Outbox;

public interface IDbContextOutbox<TDbContext> where TDbContext : DbContext
{
    void Publish<TMessage>(TMessage message)
        where TMessage : class;

    void Publish<TMessage>(TMessage message, string idempotencyKey)
        where TMessage : class;

    void Schedule<TMessage>(TMessage message, DateTime scheduledTime)
        where TMessage : class;

    void Schedule<TMessage>(TMessage message, DateTime scheduledTime, string idempotencyKey)
        where TMessage : class;

    Task<IReadOnlyList<PublishResult>> SaveChangesAndFlushMessagesAsync(CancellationToken cancellationToken = default);
}

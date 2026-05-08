using Microsoft.EntityFrameworkCore;

namespace MirageQueue.Outbox;

public interface IDbContextOutbox<TDbContext> where TDbContext : DbContext
{
    void Publish<TMessage>(TMessage message)
        where TMessage : class;

    void Schedule<TMessage>(TMessage message, DateTime scheduledTime)
        where TMessage : class;

    Task SaveChangesAndFlushMessagesAsync(CancellationToken cancellationToken = default);
}

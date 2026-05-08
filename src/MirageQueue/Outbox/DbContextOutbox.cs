using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Publishers.Abstractions;

namespace MirageQueue.Outbox;

public class DbContextOutbox<TDbContext> : IDbContextOutbox<TDbContext> where TDbContext : DbContext
{
    private readonly TDbContext _dbContext;
    private readonly IPublisher _publisher;
    private readonly List<Func<DbTransaction, CancellationToken, Task>> _pendingPublishes = new();

    public DbContextOutbox(TDbContext dbContext, IPublisher publisher)
    {
        _dbContext = dbContext;
        _publisher = publisher;
    }

    public void Publish<TMessage>(TMessage message)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);

        _pendingPublishes.Add((tx, ct) => _publisher.Publish(message, tx, ct));
    }

    public void Schedule<TMessage>(TMessage message, DateTime scheduledTime)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);

        _pendingPublishes.Add((tx, ct) => _publisher.Schedule(message, scheduledTime, tx, ct));
    }

    public async Task SaveChangesAndFlushMessagesAsync(CancellationToken cancellationToken = default)
    {
        IDbContextTransaction? transaction = _dbContext.Database.CurrentTransaction;
        var ownsTransaction = false;

        if (transaction is null)
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            ownsTransaction = true;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            var dbTransaction = transaction.GetDbTransaction();
            foreach (var publish in _pendingPublishes)
            {
                await publish(dbTransaction, cancellationToken);
            }

            _pendingPublishes.Clear();

            if (ownsTransaction)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (ownsTransaction)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            _pendingPublishes.Clear();
            throw;
        }
        finally
        {
            if (ownsTransaction)
            {
                await transaction.DisposeAsync();
            }
        }
    }
}

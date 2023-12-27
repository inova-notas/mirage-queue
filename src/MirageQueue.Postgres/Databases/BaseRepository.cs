using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MirageQueue.Common;

public abstract class BaseRepository<TContext, TEntity> : IRepository<TEntity>
    where TContext : DbContext
    where TEntity : class
{
    private readonly DbContext _dbContext;

    protected BaseRepository(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    protected virtual Task<IQueryable<TEntity>> GetQueryableAsync()
    {
        return Task.FromResult(_dbContext.Set<TEntity>().AsQueryable());
    }

    public Task<IQueryable<TEntity>> GetAllAsync()
    {
        throw new NotImplementedException();
    }

    public Task<IQueryable<TEntity>> GetAllNoTrackingAsync()
    {
        return Task.FromResult(_dbContext.Set<TEntity>().AsNoTracking());
    }

    public Task<bool> Any(Expression<Func<TEntity, bool>> predicate, IDbContextTransaction? transaction = default)
    {
        return _dbContext.Set<TEntity>().AnyAsync(predicate);
    }

    public Task Delete(TEntity entity)
    {
        _dbContext.Remove(entity);
        return Task.CompletedTask;
    }

    public Task<TEntity> InsertAsync(TEntity entity)
    {
        return Task.FromResult(_dbContext.Add(entity).Entity);
    }

    public async Task<List<TEntity>> ListWherePaginatedAsync(Expression<Func<TEntity, bool>> expression, int offset, int limit)
    {
        return await _dbContext.Set<TEntity>()
            .Where(expression)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }

    public Task<TEntity> Update(TEntity entity)
    {
        return Task.FromResult(_dbContext.Update(entity).Entity);
    }

    public Task SaveChanges()
    {
        return _dbContext.SaveChangesAsync();
    }

    public Task SetTransaction(IDbContextTransaction transaction)
    {
        return _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());
    }

    public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> expression)
    {
        return _dbContext.Set<TEntity>().FirstOrDefaultAsync(expression);
    }

    public async Task<List<TEntity>> ListWhereAsync(Expression<Func<TEntity, bool>> expression)
    {
        return await _dbContext.Set<TEntity>().Where(expression).ToListAsync();
    }
}
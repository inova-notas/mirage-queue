using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;

namespace MirageQueue.Common;

public interface IRepository<TEntity> where TEntity : class
{
    Task SaveChanges();
    Task SetTransaction(IDbContextTransaction transaction);
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> expression);
    Task<List<TEntity>> ListWhereAsync(Expression<Func<TEntity, bool>> expression);
    Task<List<TEntity>> ListWherePaginatedAsync(Expression<Func<TEntity, bool>> expression, int offset, int limit);
    Task<TEntity> InsertAsync(TEntity entity);
    Task<TEntity> Update(TEntity entity);
    Task Delete(TEntity entity);

    Task<IQueryable<TEntity>> GetAllAsync();
    Task<IQueryable<TEntity>> GetAllNoTrackingAsync();
    Task<bool> Any(Expression<Func<TEntity, bool>> predicate, IDbContextTransaction? transaction = default);
}
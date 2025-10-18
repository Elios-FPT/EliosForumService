using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Core.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(object id);
        Task<T?> GetOneAsync(
            Expression<Func<T, bool>>? filter = null,
            Expression<Func<IQueryable<T>, IOrderedQueryable<T>>>? orderBy = null,
            Expression<Func<IQueryable<T>, IQueryable<T>>>? include = null);
        Task<IEnumerable<T>> GetListAsync(
            Expression<Func<T, bool>>? filter = null,
            Expression<Func<IQueryable<T>, IOrderedQueryable<T>>>? orderBy = null,
            Expression<Func<IQueryable<T>, IQueryable<T>>>? include = null,
            int? pageSize = null,
            int? pageNumber = null);
        Task<TResult?> GetOneAsyncUntracked<TResult>(
            Expression<Func<T, bool>>? filter = null,
            Expression<Func<IQueryable<T>, IOrderedQueryable<T>>>? orderBy = null,
            Expression<Func<T, TResult>>? selector = null,
            Expression<Func<IQueryable<T>, IQueryable<T>>>? include = null);
        Task<IEnumerable<TResult>> GetListAsyncUntracked<TResult>(
            Expression<Func<T, bool>>? filter = null,
            Expression<Func<IQueryable<T>, IOrderedQueryable<T>>>? orderBy = null,
            Expression<Func<T, TResult>>? selector = null,
            Expression<Func<IQueryable<T>, IQueryable<T>>>? include = null,
            string[]? includeProperties = null,
            int? pageSize = null,
            int? pageNumber = null);
        Task<int> GetCountAsync(Expression<Func<T, bool>>? filter = null);
        Task AddAsync(T entity);
        Task AddRangeAsync(IEnumerable<T> entities);
        Task UpdateAsync(T entity);
        Task UpdateRangeAsync(IEnumerable<T> entities);
        Task DeleteAsync(T entity);
        Task DeleteRangeAsync(IEnumerable<T> entities);
        Task SaveChangesAsync();
        Task<IUnitOfWork> BeginTransactionAsync();
    }
}

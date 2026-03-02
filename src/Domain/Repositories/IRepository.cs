using Domain.Entities;

namespace Domain.Repositories
{
    public interface IRepository<T> where T : BaseEntity
    {
        Task<T?> GetByIdAsync(Guid id);
        Task<List<T>> GetAllAsync();
        Task AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(Guid id);
    }
}

namespace Domain.Repositories.Users;

using Domain.Entities;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByEmailAsync(string email);
    Task<List<User>> GetAllAsync();
    Task<User> AddAsync(User entity);
    Task UpdateAsync(User entity);
    Task DeleteAsync(int id);
    Task<bool> IsEmailExistsAsync(string email);
    Task UpdateLastLoginAsync(int userId);
    Task SaveChangesAsync();
}

using System;
using System.Threading.Tasks;
using SchoolMaster.Domain.Entities;

namespace SchoolMaster.Application.Repositories;
public interface IUserRepository
{
    Task AddUserAsync(User user);
    Task<bool> ExistsByEmailAsync(string email);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetUserByEmailAndTenantIdAsync(string email, Guid tenantId);
    Task<User?> GetUserByIdAsync(Guid userId, Guid tenantId);
    Task UpdateUserAsync(User user);
}

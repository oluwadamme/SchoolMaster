using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using SchoolMaster.Domain.Entities;

namespace SchoolMaster.Application.Repositories;
public interface IUserRepository
{
    Task AddUserAsync(User user);
    Task AddUsersBulkAsync(IEnumerable<User> users);
    Task<bool> ExistsByEmailInTenantAsync(string email, Guid tenantId);
    Task<bool> ExistsByEmailAndTenantIdAsync(string email, Guid tenantId);
    Task<HashSet<string>> GetExistingEmailsAsync(IEnumerable<string> emails, Guid tenantId);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetUserByEmailAndTenantIdAsync(string email, Guid tenantId);
    Task<User?> GetUserByIdAsync(Guid userId, Guid tenantId);
    Task UpdateUserAsync(User user);
    Task SaveChangesAsync();
}

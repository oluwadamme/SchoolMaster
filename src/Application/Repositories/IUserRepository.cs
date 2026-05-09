using System;
using System.Threading.Tasks;
using SchoolMaster.Domain.Entities;

namespace SchoolMaster.Application.Repositories;
public interface IUserRepository
{
    Task AddUserAsync(User user);
    Task<bool> ExistsByEmailAsync(string email);
}

// src/Application/Repositories/IClassRepository.cs
namespace SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;

public interface IClassRepository
{
    Task AddAsync(Class cls);
    Task<Class?> GetByIdAsync(Guid id);
    Task<List<Class>> GetAllAsync();
    Task<bool> ExistsByNameAsync(string name);
}

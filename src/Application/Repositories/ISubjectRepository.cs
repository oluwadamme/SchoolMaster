// src/Application/Repositories/ISubjectRepository.cs
namespace SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;

public interface ISubjectRepository
{
    Task AddAsync(Subject subject);
    Task<Subject?> GetByIdAsync(Guid id);
    Task<List<Subject>> GetAllAsync();
    Task<bool> ExistsByNameAsync(string name);
}

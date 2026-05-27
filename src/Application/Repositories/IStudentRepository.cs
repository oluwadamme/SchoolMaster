namespace SchoolMaster.Application.Repositories;

using SchoolMaster.Domain.Entities;
using System;
using System.Threading.Tasks;

public interface IStudentRepository
{
    Task AddStudentAsync(Student student);
    Task<bool> ExistsByStudentNumberAsync(string studentNumber, Guid tenantId);
    Task<string?> GetLastStudentNumberAsync(Guid tenantId, string prefix);
    Task SaveChangesAsync();
}
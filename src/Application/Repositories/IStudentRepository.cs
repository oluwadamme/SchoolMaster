using System;
using System.Threading.Tasks;
using SchoolMaster.Domain.Entities;

namespace SchoolMaster.Application.Repositories;

public interface IStudentRepository
{
    Task AddStudentAsync(Student student);
    Task<bool> ExistsByStudentNumberAsync(string studentNumber, Guid tenantId);
    Task SaveChangesAsync();
}
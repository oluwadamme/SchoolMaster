namespace SchoolMaster.Application.Repositories;

using SchoolMaster.Domain.Entities;
using System;
using System.Threading.Tasks;

public interface IStudentRepository
{
    Task AddStudentAsync(Student student);
    Task<bool> ExistsByStudentNumberAsync(string studentNumber, Guid tenantId);
    Task<string?> GetLastStudentNumberAsync(Guid tenantId, string prefix);
    Task<List<Student>> GetStudentsByClassIdAsync(Guid classId);
    Task<HashSet<Guid>> GetStudentIdsByClassIdAsync(Guid classId);
    Task<bool> ExistsAsync(Guid studentId);
    // IgnoreQueryFilters variant for Hangfire jobs — no HttpContext means no tenant in global filter
    Task<Student?> GetStudentByIdIgnoringFiltersAsync(Guid studentId, Guid tenantId);
}

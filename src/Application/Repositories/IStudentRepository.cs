using SchoolMaster.Domain.Entities;

namespace SchoolMaster.Application.Repositories;

public interface IStudentRepository
{
    Task<List<Student>> GetStudentsByClassIdAsync(Guid classId);
    Task<HashSet<Guid>> GetStudentIdsByClassIdAsync(Guid classId);
    Task<bool> ExistsAsync(Guid studentId);
    // IgnoreQueryFilters variant for Hangfire jobs — no HttpContext means no tenant in global filter
    Task<Student?> GetStudentByIdIgnoringFiltersAsync(Guid studentId, Guid tenantId);
}

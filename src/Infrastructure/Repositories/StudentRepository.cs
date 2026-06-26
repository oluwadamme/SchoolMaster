using Microsoft.EntityFrameworkCore;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Infrastructure.Persistence;

namespace SchoolMaster.Infrastructure.Repositories;

public class StudentRepository(SchoolMasterContext context) : IStudentRepository
{
    public async Task<List<Student>> GetStudentsByClassIdAsync(Guid classId) =>
        await context.Students
            .Where(s => s.ClassId == classId)
            .ToListAsync();

    // Projection avoids loading full entities when only IDs are needed for validation
    public async Task<HashSet<Guid>> GetStudentIdsByClassIdAsync(Guid classId) =>
        await context.Students
            .Where(s => s.ClassId == classId)
            .Select(s => s.Id)
            .ToHashSetAsync();

    public async Task<bool> ExistsAsync(Guid studentId) =>
        await context.Students.AnyAsync(s => s.Id == studentId);

    // IgnoreQueryFilters: Hangfire jobs have no HttpContext, so ICurrentTenant returns
    // Guid.Empty. Explicit tenantId param provides the isolation guarantee instead.
    public async Task<Student?> GetStudentByIdIgnoringFiltersAsync(Guid studentId, Guid tenantId) =>
        await context.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == studentId && s.TenantId == tenantId);
}

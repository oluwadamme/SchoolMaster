using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Infrastructure.Persistence;

namespace SchoolMaster.Infrastructure.Repositories;

public class StudentRepository : IStudentRepository
{
    private readonly SchoolMasterContext _context;

    public StudentRepository(SchoolMasterContext context)
    {
        _context = context;
    }

    public async Task AddStudentAsync(Student student)
    {
        await _context.Students.AddAsync(student);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsByStudentNumberAsync(string studentNumber, Guid tenantId)
    {
        return await _context.Students
            .AnyAsync(s => s.StudentNumber == studentNumber && s.TenantId == tenantId);
    }

    public async Task<string?> GetLastStudentNumberAsync(Guid tenantId, string prefix)
    {
        return await _context.Students
            .Where(s => s.TenantId == tenantId && s.StudentNumber.StartsWith(prefix))
            .OrderByDescending(s => s.StudentNumber)
            .Select(s => s.StudentNumber)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Student>> GetStudentsByClassIdAsync(Guid classId) =>
        await _context.Students
            .Where(s => s.ClassId == classId)
            .ToListAsync();

    // Projection avoids loading full entities when only IDs are needed for validation
    public async Task<HashSet<Guid>> GetStudentIdsByClassIdAsync(Guid classId) =>
        await _context.Students
            .Where(s => s.ClassId == classId)
            .Select(s => s.Id)
            .ToHashSetAsync();

    public async Task<bool> ExistsAsync(Guid studentId) =>
        await _context.Students.AnyAsync(s => s.Id == studentId);

    // IgnoreQueryFilters: Hangfire jobs have no HttpContext, so ICurrentTenant returns
    // Guid.Empty. Explicit tenantId param provides the isolation guarantee instead.
    public async Task<Student?> GetStudentByIdIgnoringFiltersAsync(Guid studentId, Guid tenantId) =>
        await _context.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == studentId && s.TenantId == tenantId);

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}

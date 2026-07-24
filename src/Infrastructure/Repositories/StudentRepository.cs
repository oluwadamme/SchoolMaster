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
    }

    public async Task AddStudentsBulkAsync(IEnumerable<Student> students)
    {
        await _context.Students.AddRangeAsync(students);
    }

    public async Task<HashSet<string>> GetExistingStudentNumbersAsync(IEnumerable<string> studentNumbers, Guid tenantId)
    {
        var numbersList = studentNumbers.ToList();
        var existing = await _context.Students
            .Where(s => s.TenantId == tenantId && numbersList.Contains(s.StudentNumber))
            .Select(s => s.StudentNumber)
            .ToListAsync();

        return new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
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
            .Include(s => s.Guardian)
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
            .Include(s => s.Guardian)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == studentId && s.TenantId == tenantId);

    public async Task<(List<Student> Items, int TotalCount)> GetAllStudentsAsync(Guid tenantId, int page, int pageSize)
    {
        var query = _context.Students
            .AsNoTracking()
            .Include(s => s.Guardian)
            .Where(s => s.TenantId == tenantId);

        var totalCount = await query.CountAsync();
        
        var items = await query
            .OrderByDescending(s => s.EnrolledAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}

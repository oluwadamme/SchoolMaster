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

    public async Task<bool> ExistsByStudentNumberAsync(string studentNumber, Guid tenantId)
    {
        return await _context.Students
            .AnyAsync(s => s.StudentNumber == studentNumber && s.TenantId == tenantId);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
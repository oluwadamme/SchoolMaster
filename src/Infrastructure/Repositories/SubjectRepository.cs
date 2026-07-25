using SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using SchoolMaster.Infrastructure.Persistence;
namespace SchoolMaster.Infrastructure.Repositories;

public class SubjectRepository : ISubjectRepository
{
    private readonly SchoolMasterContext _context;

    public SubjectRepository(SchoolMasterContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Subject subject)
    {
        await _context.Subjects.AddAsync(subject);
    }

    public async Task<bool> ExistsByNameAsync(string name)
    {
        return await _context.Subjects.AnyAsync(s => s.Name == name);
    }

    public async Task<(List<Subject> Items, int TotalCount)> GetAllAsync(int page, int pageSize)
    {
        var items = await _context.Subjects
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalCount = await _context.Subjects.CountAsync();

        return (items, totalCount);
    }

    public async Task<Subject?> GetByIdAsync(Guid id)
    {
        return await _context.Subjects.FirstOrDefaultAsync(s => s.Id == id);
    }

    public Task UpdateAsync(Subject subject)
    {
        _context.Subjects.Update(subject);
        return Task.CompletedTask;
    }
}

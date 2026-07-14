using SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SchoolMaster.Infrastructure.Repositories;
public class ClassRepository : IClassRepository
{
    private readonly SchoolMasterContext _context;

    public ClassRepository(SchoolMasterContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Class cls)
    {
        await _context.Classes.AddAsync(cls);
    }

    public async Task<bool> ExistsByNameAsync(string name)
    {
        return await _context.Classes.AnyAsync(c => c.Name == name);
    }

    public async Task<(List<Class> Items, int TotalCount)> GetAllAsync(int page, int pageSize)
    {
        var totalCount = await _context.Classes.CountAsync();
        var items = await _context.Classes
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, totalCount);
    }

    public async Task<Class?> GetByIdAsync(Guid id)
    {
        return await _context.Classes.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<Guid>> GetClassIdsByTenantAsync()
    {
        return (await _context.Classes.ToListAsync()).Select(c => c.Id).ToList();
    }

    public Task UpdateAsync(Class cls)
    {
        _context.Classes.Update(cls);
        return Task.CompletedTask;
    }
}
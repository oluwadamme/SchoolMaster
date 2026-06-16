using SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace SchoolMaster.Infrastructure.Repositories;
public class PeriodRepository : IPeriodRepository
{
    private readonly SchoolMasterContext _context;

    public PeriodRepository(SchoolMasterContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Period period)
    {
        await _context.Periods.AddAsync(period);
    }

    public async Task<Period?> GetByIdAsync(Guid id)
    {
        return await _context.Periods
            .Include(p => p.Subject)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Period>> GetPeriodsByClassAndDayAsync(Guid classId, DayOfWeek day, Guid? excludePeriodId = null)
    {
        return await _context.Periods
            .Where(p => p.ClassId == classId && p.DayOfWeek == day &&
                        (excludePeriodId == null || p.Id != excludePeriodId))
            .Include(p => p.Subject)
            .OrderBy(p => p.StartTime)
            .ToListAsync();
    }

    public async Task<List<Period>> GetPeriodsByClassIdAsync(Guid classId)
    {
        return await _context.Periods
            .Where(p => p.ClassId == classId)
            .Include(p => p.Subject)
            .OrderBy(p => p.StartTime)
            .ToListAsync();
    }

    public Task UpdateAsync(Period period)
    {
        _context.Periods.Update(period);
        return Task.CompletedTask;
    }
}
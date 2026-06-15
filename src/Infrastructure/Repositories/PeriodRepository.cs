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
        await _context.SaveChangesAsync();
    }

    public async Task<List<Period>> GetPeriodsByClassAndDayAsync(Guid classId, DayOfWeek day)
    {
        return await _context.Periods
        .Where(p => p.ClassId == classId && p.DayOfWeek == day)
        .Include(p => p.Subject)
        .OrderBy(p => p.StartTime).ToListAsync();
    }

    public async Task<List<Period>> GetPeriodsByClassIdAsync(Guid classId)
    {
        return await _context.Periods
        .Where(p => p.ClassId == classId)
        .Include(p => p.Subject)
        .OrderBy(p => p.StartTime).ToListAsync();
    }
}
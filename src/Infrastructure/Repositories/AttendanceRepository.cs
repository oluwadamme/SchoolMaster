using Microsoft.EntityFrameworkCore;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Infrastructure.Persistence;

namespace SchoolMaster.Infrastructure.Repositories;

public class AttendanceRepository(SchoolMasterContext context) : IAttendanceRepository
{
    public async Task<List<DailyAttendance>> GetByClassAndDateAsync(Guid classId, DateOnly date) =>
        await context.DailyAttendances
            .Where(a => a.ClassId == classId && a.Date == date)
            .ToListAsync();

    public async Task<List<DailyAttendance>> GetByStudentAsync(
        Guid studentId, Guid? termId, DateOnly? from, DateOnly? to)
    {
        var query = context.DailyAttendances
            .Where(a => a.StudentId == studentId);

        if (termId.HasValue) query = query.Where(a => a.TermId == termId.Value);
        if (from.HasValue)   query = query.Where(a => a.Date >= from.Value);
        if (to.HasValue)     query = query.Where(a => a.Date <= to.Value);

        return await query.OrderBy(a => a.Date).ToListAsync();
    }

    public async Task AddRangeAsync(List<DailyAttendance> records) =>
        await context.DailyAttendances.AddRangeAsync(records);
}

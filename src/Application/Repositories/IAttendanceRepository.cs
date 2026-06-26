
using SchoolMaster.Domain.Entities;

namespace SchoolMaster.Application.Repositories;

public interface IAttendanceRepository
{
    Task<List<DailyAttendance>> GetByClassAndDateAsync(Guid classId, DateOnly date);
    Task<List<DailyAttendance>> GetByStudentAsync(Guid studentId, Guid? termId, DateOnly? from, DateOnly? to);
    Task AddRangeAsync(List<DailyAttendance> records);
}

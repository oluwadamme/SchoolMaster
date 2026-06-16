// src/Application/Repositories/IPeriodRepository.cs
namespace SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;

public interface IPeriodRepository
{
    Task AddAsync(Period period);
    Task<Period?> GetByIdAsync(Guid id);
    Task<List<Period>> GetPeriodsByClassIdAsync(Guid classId);
    Task<List<Period>> GetPeriodsByClassAndDayAsync(Guid classId, DayOfWeek day, Guid? excludePeriodId = null);
    Task UpdateAsync(Period period);
}

// src/Application/Repositories/IPeriodRepository.cs
namespace SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;

public interface IPeriodRepository
{
    Task AddAsync(Period period);
    Task<List<Period>> GetByClassIdAsync(Guid classId);
    Task<List<Period>> GetByClassAndDayAsync(Guid classId, DayOfWeek day);
}

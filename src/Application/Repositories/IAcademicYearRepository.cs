// src/Application/Repositories/IAcademicYearRepository.cs
namespace SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;

public interface IAcademicYearRepository
{
    Task AddAsync(AcademicYear year);
    Task<AcademicYear?> GetByIdAsync(Guid id);
    Task<AcademicYear?> GetCurrentAsync();
    Task<(List<AcademicYear> Items, int TotalCount)> GetAllAsync(int page, int pageSize);
    Task<bool> ExistsByNameAsync(string name);
    Task UpdateAsync(AcademicYear year);

    Task AddTermAsync(Term term);
    Task<Term?> GetTermByIdAsync(Guid termId);
    Task<Term?> GetCurrentTermAsync(Guid academicYearId);
    Task<List<Term>> GetTermsByYearAsync(Guid academicYearId);
    Task UpdateTermAsync(Term term);
}

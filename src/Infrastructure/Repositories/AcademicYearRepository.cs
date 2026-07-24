// src/Infrastructure/Repositories/AcademicYearRepository.cs
using SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class AcademicYearRepository : IAcademicYearRepository
{
    private readonly SchoolMasterContext _context;

    public AcademicYearRepository(SchoolMasterContext context) => _context = context;

    public async Task AddAsync(AcademicYear year)
    {
        await _context.AcademicYears.AddAsync(year);
    }

    public async Task<AcademicYear?> GetByIdAsync(Guid id) =>
        await _context.AcademicYears.FirstOrDefaultAsync(y => y.Id == id);

    public async Task<AcademicYear?> GetCurrentAsync() =>
        await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);

    public async Task<(List<AcademicYear> Items, int TotalCount)> GetAllAsync(int page, int pageSize)
    {
        var totalCount = await _context.AcademicYears.CountAsync();
        var items = await _context.AcademicYears
            .OrderByDescending(y => y.StartDate)
            // each page contains pageSize number of academic years
            // this skips pages depending on your preference. i.e if you set Page = 2 and PageSize = 10
            // it skips one page AKA 10 academic years since a Page has PageSize number of items
            .Skip((page - 1) * pageSize)
            // then takes the next page(pageSize amount of items)
            .Take(pageSize)
            .ToListAsync();
        return (items, totalCount);
    }

    public async Task<bool> ExistsByNameAsync(string name) =>
        await _context.AcademicYears.AnyAsync(y => y.Name == name);

    public async Task UpdateAsync(AcademicYear year)
    {
        _context.AcademicYears.Update(year);
    }

    public async Task AddTermAsync(Term term)
    {
        await _context.Terms.AddAsync(term);
    }

    public async Task<Term?> GetTermByIdAsync(Guid termId) =>
        await _context.Terms.FirstOrDefaultAsync(t => t.Id == termId);

    public async Task<Term?> GetCurrentTermAsync(Guid academicYearId) =>
        await _context.Terms.FirstOrDefaultAsync(t => t.AcademicYearId == academicYearId && t.IsCurrent);

    public async Task<List<Term>> GetTermsByYearAsync(Guid academicYearId) =>
        await _context.Terms
            .Where(t => t.AcademicYearId == academicYearId)
            .OrderBy(t => t.TermNumber)
            .ToListAsync();

    public Task UpdateTermAsync(Term term)
    {
        _context.Terms.Update(term);
        return Task.CompletedTask;
    }
}

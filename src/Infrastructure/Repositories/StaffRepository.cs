using Microsoft.EntityFrameworkCore;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Infrastructure.Persistence;
using System;
using System.Threading.Tasks;

namespace SchoolMaster.Infrastructure.Repositories;

public class StaffRepository : IStaffRepository
{
    private readonly SchoolMasterContext _context;

    public StaffRepository(SchoolMasterContext context)
    {
        _context = context;
    }

    public async Task AddStaffAsync(Staff staff)
    {
        await _context.Staff.AddAsync(staff);
    }

    public async Task AddStaffBulkAsync(IEnumerable<Staff> staff)
    {
        await _context.Staff.AddRangeAsync(staff);
    }

    public async Task<bool> ExistsByStaffNumberAsync(string staffNumber, Guid tenantId) =>
        await _context.Staff.AnyAsync(s => s.StaffNumber == staffNumber && s.TenantId == tenantId);

    public async Task<string?> GetLastStaffNumberAsync(Guid tenantId, string prefix)
    {
        return await _context.Staff
            .Where(s => s.TenantId == tenantId && s.StaffNumber.StartsWith(prefix))
            .OrderByDescending(s => s.StaffNumber)
            .Select(s => s.StaffNumber)
            .FirstOrDefaultAsync();
    }
    
    public async Task<Staff?> GetStaffByIdAsync(Guid staffId)
    {
        return await _context.Staff.FirstOrDefaultAsync(s => s.Id == staffId);
    }
}

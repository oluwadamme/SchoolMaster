namespace SchoolMaster.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Infrastructure.Persistence;
using System;
using System.Threading.Tasks;

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
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsByStaffNumberAsync(string staffNumber, Guid tenantId) =>
        await _context.Staff.AnyAsync(s => s.StaffNumber == staffNumber && s.TenantId == tenantId);
}
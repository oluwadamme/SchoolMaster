using Microsoft.EntityFrameworkCore;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Infrastructure.Persistence;

namespace SchoolMaster.Infrastructure.Repositories;

public class GuardianRepository : IGuardianRepository
{
    private readonly SchoolMasterContext _context;

    public GuardianRepository(SchoolMasterContext context)
    {
        _context = context;
    }

    public async Task AddGuardianAsync(Guardian guardian)
    {
        await _context.Guardians.AddAsync(guardian);
    }

    public async Task AddGuardiansBulkAsync(IEnumerable<Guardian> guardians)
    {
        await _context.Guardians.AddRangeAsync(guardians);
    }

    public async Task<Guardian?> GetGuardianByEmailAsync(string email, Guid tenantId)
    {
        return await _context.Guardians
            .FirstOrDefaultAsync(g => g.Email == email && g.TenantId == tenantId);
    }

    public async Task<List<Guardian>> GetGuardiansByEmailsAsync(IEnumerable<string> emails, Guid tenantId)
    {
        var emailList = emails.Select(e => e.ToLowerInvariant()).ToList();
        return await _context.Guardians
            .Where(g => g.TenantId == tenantId && emailList.Contains(g.Email.ToLower()))
            .ToListAsync();
    }

    public async Task<Guardian?> GetGuardianByIdAsync(Guid id, Guid tenantId)
    {
        return await _context.Guardians
            .FirstOrDefaultAsync(g => g.Id == id && g.TenantId == tenantId);
    }
}

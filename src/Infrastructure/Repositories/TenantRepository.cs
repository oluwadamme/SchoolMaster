namespace SchoolMaster.Infrastructure.Repositories;

using System;
using Microsoft.EntityFrameworkCore;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Infrastructure.Persistence;
using SchoolMaster.Domain.Entities;

public class TenantRepository : ITenantRepository
{
    private readonly SchoolMasterContext _context;

    public TenantRepository(SchoolMasterContext context)
    {
        _context = context;
    }

    public async Task AddTenantAsync(Tenant tenant)
    {
        await _context.Tenants.AddAsync(tenant);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsBySubdomainAsync(string subdomain)
    {
        return await _context.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Subdomain == subdomain);
    }

    public async Task<Tenant?> GetTenantBySubdomainAsync(string subdomain)
    {
<<<<<<< HEAD
        return await _context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Subdomain == subdomain);
=======
        return await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Subdomain == subdomain);
>>>>>>> c3b83190ef06c4dfb48c9f0a4d16c9447aaebd0a
    }
}
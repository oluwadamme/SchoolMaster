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
        return await _context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Subdomain == subdomain);

    }

    public async Task<Tenant?> GetByIdAsync(Guid id)
    {
        return await _context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }


}
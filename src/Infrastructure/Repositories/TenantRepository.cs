using System;

using Microsoft.EntityFrameworkCore;

public class TenantRepository : ITenantRepository
{
    private readonly SchoolMasterContext _context;

    public TenantRepository(SchoolMasterContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Tenant tenant)
    {
        await _context.Tenants.AddAsync(tenant);
        await _context.SaveChangesAsync();
    }
}
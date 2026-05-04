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

    public async Task AddAsync(Tenant tenant)
    {
        await _context.Tenants.AddAsync(tenant);
        await _context.SaveChangesAsync();
    }
}
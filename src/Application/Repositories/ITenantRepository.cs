using System;
using SchoolMaster.Domain.Entities;

namespace SchoolMaster.Application.Repositories;

public interface ITenantRepository
{
    Task AddTenantAsync(Tenant tenant);
    Task<bool> ExistsBySubdomainAsync(string subdomain);
    Task<Tenant?> GetTenantBySubdomainAsync(string subdomain);
}
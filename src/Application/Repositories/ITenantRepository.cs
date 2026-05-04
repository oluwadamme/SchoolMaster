using System;
namespace SchoolMaster.Application.Repositories;

public interface ITenantRepository
{
    Task AddAsync(Tenant tenant);
}
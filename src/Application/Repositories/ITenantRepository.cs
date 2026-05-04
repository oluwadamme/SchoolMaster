using System;
using SchoolMaster.Domain.Entities;

namespace SchoolMaster.Application.Repositories;

public interface ITenantRepository
{
    Task AddAsync(Tenant tenant);
}
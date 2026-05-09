using System;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using SchoolMaster.Application.DTOs;

namespace SchoolMaster.Application.Services;

public class OnboardingService : IOnboardingService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRepository _userRepository;

    public OnboardingService(
        ITenantRepository tenantRepository,
        IUserRepository userRepository)
    {
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
    }

    public async Task<BaseResponse<Guid>> CreateTenantWithAdminAsync(OnboardTenantRequest request)
    {
        // 1. Check admin email uniqueness
        if (await _userRepository.ExistsByEmailAsync(request.AdminEmail))
        {
            throw new ArgumentException("Admin email already exists.");
        }

        if (await _tenantRepository.ExistsBySubdomainAsync(request.Subdomain))
        {
            throw new ArgumentException("Subdomain already exists.");
        }

        // 2. Create Tenant
        var tenant = new Tenant(Guid.NewGuid(), request.SchoolName, request.Subdomain, request.ContactEmail, TenantStatus.Active, TenantPlan.Basic, DateTime.UtcNow);

        await _tenantRepository.AddTenantAsync(tenant);

        // 3. Create Admin User (linked to tenant)
        var adminUser = new User(Guid.NewGuid(), tenant.Id, request.AdminFirstName, request.AdminLastName, request.AdminEmail, BCrypt.Net.BCrypt.HashPassword(request.AdminPassword), UserRole.Admin, false, DateTime.UtcNow);

        await _userRepository.AddUserAsync(adminUser);


        // 4. Return tenantId
        return BaseResponse<Guid>.SuccessResponse(
            "Tenant and Admin created successfully",
            tenant.Id
        );
    }
}
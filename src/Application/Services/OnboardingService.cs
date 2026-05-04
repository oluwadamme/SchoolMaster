using System;
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
            return BaseResponse<Guid>.ErrorResponse("Admin email already exists");
        }

        // 2. Create Tenant
        var tenant = new Tenant(
            Guid.NewGuid(),
            request.SchoolName,
            request.Subdomain,
            request.ContactEmail,
            TenantStatus.Active,
            TenantPlan.Basic,
            DateTime.UtcNow
        );

        await _tenantRepository.AddAsync(tenant);

        // 3. Create Admin User (linked to tenant)
        var adminUser = new User(
            Guid.NewGuid(),
            tenant.Id,
            request.AdminEmail,
            BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
            UserRole.Admin,
            true,
            null,
            null,
            null,
            null,
            DateTime.UtcNow
        );

        await _userRepository.AddAsync(adminUser);

        // 4. Save everything in ONE transaction

        // 5. Return tenantId
        return BaseResponse<Guid>.SuccessResponse(
            "Tenant and Admin created successfully",
            tenant.Id
        );
    }
}
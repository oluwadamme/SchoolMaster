using System;

namespace SchoolMaster.Application.Services.Interfaces;

public interface IOnboardingService
{
    Task<BaseResponse<Guid>> CreateTenantWithAdminAsync(OnboardTenantRequest request);
}
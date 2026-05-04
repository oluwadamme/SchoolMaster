using Microsoft.AspNetCore.Mvc;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Services.Interfaces;

namespace SchoolMaster.Api.Controllers;

//“Create my school and make me the admin”
[ApiController]
[Route("api/onboarding")]
public class OnboardingController : ControllerBase
{
    private readonly IOnboardingService _onboardingService;

    public OnboardingController(IOnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> OnboardTenant(OnboardTenantRequest request)
    {
        var result = await _onboardingService.CreateTenantWithAdminAsync(request);
        return CreatedAtAction(nameof(OnboardTenant), new { id = result.Data }, result);
    }
}
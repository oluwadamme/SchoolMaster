
using Microsoft.AspNetCore.Mvc;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Services.Interfaces;

//“Create my school and make me the admin”
[ApiController]
[Route("api/[controller]")]
public class OnboardingController : ControllerBase
{
    private readonly IOnboardingService _onboardingService;

    public OnboardingController(IOnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }

    [HttpPost("tenants")]
    public async Task<ActionResult> OnboardTenant([FromBody] OnboardTenantRequest request)
    {
        var result = await _onboardingService.CreateTenantWithAdminAsync(request);
        return CreatedAtAction(nameof(OnboardTenant), new { id = result.Data }, result);
    }

    [HttpPost("verify-email")]
    public async Task<ActionResult> VerifyUserEmail([FromBody] VerifyUserEmailRequest request)
    {
        var result = await _onboardingService.VerifyUserEmailAsync(request);
        return Ok(result);
    }

    [HttpPost("resend-verification-token")]
    public async Task<ActionResult> ResendVerificationToken([FromBody] ResendOtpRequest request)
    {
        var result = await _onboardingService.ResendVerificationTokenAsync(request);
        return Ok(result);
    }


}
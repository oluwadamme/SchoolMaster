
using Microsoft.AspNetCore.Mvc;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Services.Interfaces;
using Microsoft.AspNetCore.RateLimiting;

namespace SchoolMaster.Api.Controllers;

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

    [EnableRateLimiting("AuthLimit")]
    [HttpPost("verify-email")]
    public async Task<ActionResult> VerifyUserEmail([FromBody] VerifyUserEmailRequest request)
    {
        var result = await _onboardingService.VerifyUserEmailAsync(request);
        return Ok(result);
    }

    [EnableRateLimiting("AuthLimit")]
    [HttpPost("resend-verification-otp")]
    public async Task<ActionResult> ResendVerificationOtp([FromBody] ResendOtpRequest request)
    {
        var result = await _onboardingService.ResendVerificationOtpAsync(request);
        return Ok(result);
    }


}
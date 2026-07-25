
using Microsoft.AspNetCore.Mvc;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Services.Interfaces;
using Microsoft.AspNetCore.RateLimiting;

namespace SchoolMaster.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class OnboardingController : ControllerBase
{
    private readonly IOnboardingService _onboardingService;

    public OnboardingController(IOnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }
    [EnableRateLimiting("AuthLimit")]
    [HttpPost("tenants")]
    public async Task<ActionResult<BaseResponse<OnboardTenantResponse>>> OnboardTenant([FromBody] OnboardTenantRequest request)
    {
        var result = await _onboardingService.CreateTenantWithAdminAsync(request);
        return CreatedAtAction(nameof(OnboardTenant), new { id = result.Data?.TenantId }, result);
    }

    [EnableRateLimiting("AuthLimit")]
    [HttpPost("verify-email")]
    public async Task<ActionResult<BaseResponse<bool>>> VerifyUserEmail([FromBody] VerifyUserEmailRequest request)
    {
        var result = await _onboardingService.VerifyUserEmailAsync(request);
        return Ok(result);
    }

    [EnableRateLimiting("AuthLimit")]
    [HttpPost("resend-verification-otp")]
    public async Task<ActionResult<BaseResponse<bool>>> ResendVerificationOtp([FromBody] ResendOtpRequest request)
    {
        var result = await _onboardingService.ResendVerificationOtpAsync(request);
        return Ok(result);
    }


}
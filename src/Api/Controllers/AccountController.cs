using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Services.Interfaces;

namespace SchoolMaster.Api.Controllers;

[ApiController]
[Route("api/v1/account")]
// Needs a valid JWT token. 
[Authorize]
// a dedicated place for a user to manage their personal account settings and actions, like verifying email, changing password, or updating profile picture
public class AccountController : ControllerBase
{
    private readonly IOnboardingService _onboardingService;

    public AccountController(IOnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }

    [HttpPost("verify-email")]
    public async Task<ActionResult<BaseResponse<bool>>> VerifyEmail([FromBody] VerifyUserEmailRequest request)
    {
        var result = await _onboardingService.VerifyUserEmailAsync(request);
        return Ok(result);
    }
}
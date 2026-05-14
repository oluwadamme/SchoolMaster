using Microsoft.AspNetCore.Mvc;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace SchoolMaster.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentTenant _currentTenant;


    public AuthController(IAuthService authService, ICurrentTenant currentTenant)
    {
        _authService = authService;
        _currentTenant = currentTenant;
    }

    [HttpPost("login")]
    public async Task<ActionResult<BaseResponse<AuthResponse>>> Login([FromBody] LoginRequest request)
    {
        // The Controller receives the request and gives it to the AuthService.
        var response = await _authService.LoginAsync(request);
        // If the login worked, we send the token and a 200 (OK) status code.
        return Ok(response);
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<BaseResponse<AuthResponse>>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        // The Controller receives the refresh request and gives it to the AuthService.
        var response = await _authService.RefreshTokenAsync(request);

        // If the refresh failed (e.g., token expired or invalid), we send a 401.
        // The ExceptionMiddleware will handle specific exceptions like InvalidCredentialsException.
        // We just return Ok here, and the middleware will convert exceptions to appropriate HTTP responses.
        return Ok(response);
    }

      /// <summary>
    /// Deactivates a user account using their email address.
    /// </summary>
    [HttpPatch("users/deactivate-by-email")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BaseResponse<bool>>> DeactivateByEmail([FromBody] DeactivateUserByEmailRequest request)
    {
        var result = await _authService.DeactivateUserByEmailAsync(request.Email, _currentTenant.Id);
        return Ok(result);
    }

}
using Microsoft.AspNetCore.Mvc;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Services.Interfaces;

namespace SchoolMaster.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<BaseResponse<AuthResponse>>> Login(LoginRequest request)
    {
        // The Controller receives the request and gives it to the AuthService.
        var response = await _authService.LoginAsync(request);

        // If the login failed, we send a 401 (Unauthorized) status code.
        if (!response.Success)
        {
            return Unauthorized(response);
        }

        // If the login worked, we send the token and a 200 (OK) status code.
        return Ok(response);
    }
}
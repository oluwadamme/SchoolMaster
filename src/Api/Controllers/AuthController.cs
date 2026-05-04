using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace SchoolMaster.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }


}
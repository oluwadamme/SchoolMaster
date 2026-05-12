using SchoolMaster.Application.DTOs;

namespace SchoolMaster.Application.Services.Interfaces;

/// <summary>
/// This is the rule book for Authentication. 
/// It says that any service doing "Auth" must have a Login task.
/// </summary>
public interface IAuthService
{
    // This task takes a LoginRequest and gives back a BaseResponse with the AuthResponse inside.
    Task<BaseResponse<AuthResponse>> LoginAsync(LoginRequest request);
}
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.CustomException;

namespace SchoolMaster.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;

    public AuthService(IUserRepository userRepository, IJwtService jwtService)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
    }

    public async Task<BaseResponse<AuthResponse>> LoginAsync(LoginRequest request)
    {
        // 1. Find the user in the database using their email.
        // The IUserRepository tool helps us do this.
        var user = await _userRepository.GetUserByEmailAsync(request.Email);

        // If no user is found with that email, it means the email is wrong.
        if (user == null)
        {
            throw new UserNotFoundException("Invalid email or password.");
        }

        // 2. Check if the password is correct.
        // We use BCrypt to compare the typed password with the scrambled one (Hash) in the database.
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!isPasswordValid)
        {
            throw new InvalidCredentialsException("Invalid email or password.");
        }

        // 3. Create the two types of tokens.
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        // 4. Save the Refresh Token to the database.
        // We set it to live for 7 days.
        user.UpdateRefreshToken(refreshToken, 7);
        await _userRepository.UpdateUserAsync(user);

        // 5. Put everything into the Response container and send it back.
        var authResponse = new AuthResponse(
            accessToken,
            refreshToken,
            user.Id,
            user.TenantId,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role,
            user.IsEmailVerified
        );

        return BaseResponse<AuthResponse>.SuccessResponse("Login successful.", authResponse);
    }

    public async Task<BaseResponse<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request)
    {
        // 1. Read the dead Access Token to get the user's ID.
        // We use the IJwtService tool to do this.
        var principal = _jwtService.GetPrincipalFromExpiredToken(request.AccessToken);

        // 2. Get the user's ID from the dead token.
        // The NameIdentifier claim holds the user's unique ID.
        var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            throw new InvalidCredentialsException("Invalid token claims.");
        }
        var userId = Guid.Parse(userIdClaim.Value);

        // 3. Get the user's Tenant ID from the dead token.
        // This is important for multi-tenancy.
        var tenantIdClaim = principal.Claims.FirstOrDefault(c => c.Type == "tenant_id");
        if (tenantIdClaim == null)
        {
            throw new InvalidCredentialsException("Invalid token claims.");
        }
        var tenantId = Guid.Parse(tenantIdClaim.Value);

        // 4. Find the user in the database using the ID from the dead token.
        var user = await _userRepository.GetUserByIdAsync(userId, tenantId);
        if (user == null)
        {
            throw new UserNotFoundException("User not found.");
        }

        // 5. Check if the Refresh Token from the request matches the one stored in the database.
        if (user.RefreshToken != request.RefreshToken)
        {
            throw new InvalidCredentialsException("Invalid refresh token.");
        }

        // 6. Check if the Refresh Token has expired.
        if (user.RefreshTokenExpiry <= DateTime.UtcNow)
        {
            throw new InvalidCredentialsException("Refresh token expired.");
        }

        // 7. If all checks pass, create new tokens.
        var newAccessToken = _jwtService.GenerateAccessToken(user);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        // 8. Update the user's Refresh Token in the database.
        user.UpdateRefreshToken(newRefreshToken, 7); // Give the new refresh token 7 days.
        await _userRepository.UpdateUserAsync(user);

        // 9. Send back the new tokens and user details.
        var authResponse = new AuthResponse(newAccessToken, newRefreshToken, user.Id, user.TenantId, user.Email, user.FirstName, user.LastName, user.Role, user.IsEmailVerified);

        return BaseResponse<AuthResponse>.SuccessResponse("Token refreshed successfully.", authResponse);
    }

  
    public async Task<BaseResponse<bool>> DeactivateUserByEmailAsync(string email, Guid tenantId)
    {
        // 1. Find the user by Email and TenantId (Safety first!)
        var user = await _userRepository.GetUserByEmailAndTenantIdAsync(email, tenantId);

        // If we can't find them, they might be in another school or already inactive
        if (user == null)
        {
            throw new UserNotFoundException("Active user with this email not found in your school.");
        }

        // 2. Flip the switch to Inactive
        user.Deactivate();

        // 3. Save the change
        await _userRepository.UpdateUserAsync(user);

        return BaseResponse<bool>.SuccessResponse("User deactivated successfully.", true);
    }
}
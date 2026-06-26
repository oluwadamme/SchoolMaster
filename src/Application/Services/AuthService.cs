using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.CustomException;
using SchoolMaster.Domain.Enums;
using Serilog;
using Hangfire;
using Microsoft.Extensions.Options;
using SchoolMaster.Infrastructure.Options;

namespace SchoolMaster.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IOptions<EmailVerificationOptions> _emailOptions;
    private readonly ICurrentTenant _currentTenant;
    private readonly IOtpService _otpService;

    public AuthService(
        IUserRepository userRepository, 
        IJwtService jwtService, 
        IBackgroundJobClient backgroundJobClient, 
        IOptions<EmailVerificationOptions> emailOptions, 
        ICurrentTenant currentTenant, 
        IOtpService otpService)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _backgroundJobClient = backgroundJobClient;
        _emailOptions = emailOptions;
        _currentTenant = currentTenant;
        _otpService = otpService;
    }

    public async Task<BaseResponse<AuthResponse>> LoginAsync(LoginRequest request)
    {
        // 1. Find the user by email. Tenant scoping is applied by the global query filter.
        var user = await _userRepository.GetUserByEmailAsync(request.Email);

        // Unknown email and wrong password return the same error (and status) so an attacker cannot use
        // login responses to discover which emails are registered.
        if (user == null)
        {
            throw new InvalidCredentialsException("Invalid email or password.");
        }

        // Account-level brute-force protection: once locked, reject even a correct password until the
        // lockout window passes. This complements the per-IP rate limiter on the endpoint.
        if (user.IsLockedOut())
        {
            throw new AccountLockedException(
                "Account temporarily locked due to too many failed login attempts. Please try again later.");
        }

        // Account-level brute-force protection: once locked, reject even a correct password until the
        // lockout window passes. This complements the per-IP rate limiter on the endpoint.
        if (user.IsLockedOut())
        {
            throw new AccountLockedException(
                "Account temporarily locked due to too many failed login attempts. Please try again later.");
        }

        // 2. Check if the password is correct.
        // We use BCrypt to compare the typed password with the scrambled one (Hash) in the database.
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!isPasswordValid)
        {
            user.RegisterFailedLogin();
            await _userRepository.UpdateUserAsync(user);
            throw new InvalidCredentialsException("Invalid email or password.");
        }

        // 3. Gate on account status — only AFTER the password is verified, so we never reveal an
        // account's state to someone who has not proven they own it.
        switch (user.Status)
        {
            case UserStatus.Active:
                break;
            case UserStatus.PendingVerification:
                throw new EmailNotVerifiedException("Please verify your email address before logging in.");
            case UserStatus.Inactive:
                throw new AccountInactiveException(
                    "Your account has been deactivated. Please contact your administrator.");
            case UserStatus.Suspended:
                throw new AccountInactiveException(
                    "Your account has been suspended. Please contact your administrator.");
        }

        // 3. Create the two types of tokens.
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        // 4. Clear any failed-login state and save the new Refresh Token to the database.
        // We set it to live for 7 days.
        user.RegisterSuccessfulLogin();
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
            user.Roles,
            user.IsEmailVerified
        );

        return BaseResponse<AuthResponse>.SuccessResponse("Login successful.", authResponse);
    }

    public async Task<BaseResponse<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request)
    {
        // 1. Read the dead Access Token to get the user's ID.
        // We use the IJwtService tool to do this.
        var principal = _jwtService.GetPrincipalFromExpiredToken(request.AccessToken);

        // 2. Get the user's ID from the principal.
        // The NameIdentifier claim holds the user's unique ID.
        var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            throw new InvalidCredentialsException("Invalid token claims.");
        }
        var userId = Guid.Parse(userIdClaim.Value);

        // 3. Get the user's Tenant ID from the principal.
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
        var authResponse = new AuthResponse(newAccessToken, newRefreshToken, user.Id, user.TenantId, user.Email, user.FirstName, user.LastName, user.Roles, user.IsEmailVerified);

        return BaseResponse<AuthResponse>.SuccessResponse("Token refreshed successfully.", authResponse);
    }

  
    public async Task<BaseResponse<bool>> DeactivateUserByEmailAsync(string email)
    {
        // Tenant scoping is applied by the global query filter (the caller is authenticated).
        var user = await _userRepository.GetUserByEmailAsync(email);

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

    public async Task<BaseResponse<bool>> ForgotPasswordAsync(ForgetPasswordRequest request)
    {
        // 1. Get the TenantId automatically from our abstraction!
        var tenantId = _currentTenant.Id;
        if (tenantId == Guid.Empty)
        {
            Log.Warning("Password flow invoked with no resolved tenant.");

            return BaseResponse<bool>.SuccessResponse("Forgot password token sent successfully", true);
        }
        var user = await _userRepository.GetUserByEmailAsync(request.Email);
        if (user == null)
        {
            Log.Warning("Password flow target not found in tenant {TenantId}.", tenantId);
            return BaseResponse<bool>.SuccessResponse("Forgot password token sent successfully", true);
        }
        var otp = _otpService.GenerateVerificationOtp();
        var subject = "Forgot your password";
        var body = $"Hello {user.FirstName},\n\nForgot your password? Use the code below to reset it:\n\n{otp}\n\nRegards,\n\nSchoolMaster Team";

        user.OtpToken = otp;
        user.OtpExpiry = DateTime.UtcNow.AddMinutes(_emailOptions.Value.ExpirationInMinutes);
        user.OtpAttemptCount = 0; // fresh OTP starts with a clean attempt budget
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateUserAsync(user);

        _backgroundJobClient.Enqueue<IEmailService>(x =>
        x.SendEmailAsync(user.Email, user.FirstName, subject, body));

        return BaseResponse<bool>.SuccessResponse("Forgot password token sent successfully", true);
    }
    public async Task<BaseResponse<bool>> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var tenantId = _currentTenant.Id;
        if (tenantId == Guid.Empty)
        {
            Log.Warning("Password flow invoked with no resolved tenant.");

            throw new InvalidOtpException("Invalid OTP or Email address.");
        }
        var user = await _userRepository.GetUserByEmailAsync(request.Email);
        if (user == null || user.OtpToken == null || user.OtpToken != request.Otp)
        {
            Log.Warning("Password flow target not found in tenant {TenantId}.", tenantId);

            // Count the wrong guess against the account and wipe the OTP once the budget is exhausted,
            // so a 6-digit code cannot be brute-forced within its lifetime.
            if (user is { OtpToken: not null })
            {
                user.RegisterFailedOtpAttempt();
                await _userRepository.UpdateUserAsync(user);
            }

            throw new InvalidOtpException("Invalid OTP or Email address.");
        }
        if (user.OtpExpiry < DateTime.UtcNow)
        {
            throw new OtpExpiredException("OTP has expired. Please request a new one.");
        }
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        user.ClearOtp();
        // Reset invalidates every existing session: rotate the stamp (kills access tokens) and drop the
        // refresh token. Otherwise a thief who triggered the reset, or a stale session, would survive it.
        user.RotateSecurityStamp();
        user.ClearRefreshToken();
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateUserAsync(user);
        return BaseResponse<bool>.SuccessResponse("Password reset successfully", true);
    }
}
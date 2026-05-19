using System;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using SchoolMaster.Application.DTOs;
using Serilog;
using Microsoft.Extensions.Options;
using SchoolMaster.Infrastructure.Options;
using Hangfire;
using System.Security.Cryptography;
using SchoolMaster.Domain.CustomException;
using System.Transactions;

namespace SchoolMaster.Application.Services;

public class OnboardingService : IOnboardingService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRepository _userRepository;
    private readonly IOptions<EmailVerificationOptions> _emailOptions;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ICurrentTenant _currentTenant;

    public OnboardingService(
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IOptions<EmailVerificationOptions> emailOptions,
        IBackgroundJobClient backgroundJobClient,
        ICurrentTenant currentTenant)
    {
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _emailOptions = emailOptions;
        _backgroundJobClient = backgroundJobClient;
        _currentTenant = currentTenant;
    }

    public async Task<BaseResponse<Guid>> CreateTenantWithAdminAsync(OnboardTenantRequest request)
    {
        // 1. Check admin email uniqueness
        if (await _userRepository.ExistsByEmailAsync(request.AdminEmail))
        {
            throw new AlreadyExistException("Admin email already exists.");
        }

        if (await _tenantRepository.ExistsBySubdomainAsync(request.Subdomain))
        {
            throw new AlreadyExistException("Subdomain already exists.");
        }

        // Use transaction scope to ensure atomicity
        using var dbScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        // 2.Create Tenant
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.SchoolName,
            Subdomain = request.Subdomain,
            ContactEmail = request.ContactEmail,
            Status = TenantStatus.Active,
            Plan = TenantPlan.Basic,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _tenantRepository.AddTenantAsync(tenant);

        var otp = GenerateVerificationOtp();
        var subject = "Verify your email";
        var body = $"Hello {request.AdminFirstName},\n\nThanks for registering with SchoolMaster!\n\nPlease verify your email by using the code below: {otp}\n\nRegards,\n\nSchoolMaster Team";


        // 3. Create Admin User (linked to tenant)
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            FirstName = request.AdminFirstName,
            LastName = request.AdminLastName,
            Email = request.AdminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
            Role = UserRole.Admin,
            IsEmailVerified = false,
            OtpToken = otp,
            OtpExpiry = DateTime.UtcNow.AddMinutes(_emailOptions.Value.ExpirationInMinutes),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepository.AddUserAsync(adminUser);
        dbScope.Complete();
        // 4. Send email verification otp

        _backgroundJobClient.Enqueue<IEmailService>(x =>
        x.SendEmailAsync(request.AdminEmail, request.AdminFirstName, subject, body));

        // 5. Return tenantId
        return BaseResponse<Guid>.SuccessResponse(
            "Tenant and Admin created successfully",
            tenant.Id
        );
    }
    private string GenerateVerificationOtp()
    {
        // generate 4 digit otp, if it is development env, the code will be 0000 else it will generate random code
        // if (Environment.IsDevelopment())
        // {
        //     return "0000";
        // }
        var token = RandomNumberGenerator.GetInt32(10000).ToString("D4");
        return token;
    }

    public async Task<BaseResponse<bool>> VerifyUserEmailAsync(VerifyUserEmailRequest request)
    {
        var user = await _userRepository.GetUserByEmailAndTenantIdAsync(request.Email, request.TenantId);
        if (user == null)
        {
            throw new UserNotFoundException("User not found.");
        }
        if (user.OtpToken != request.OtpToken)
        {
            throw new InvalidOtpException("Invalid OTP.");
        }
        if (user.OtpExpiry < DateTime.UtcNow)
        {
            throw new OtpExpiredException("OTP expired.");
        }
        user.IsEmailVerified = true;
        user.OtpToken = null;
        user.OtpExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateUserAsync(user);
        return BaseResponse<bool>.SuccessResponse("Email verified successfully", true);
    }

    public async Task<BaseResponse<bool>> ResendVerificationOtpAsync(ResendOtpRequest request)
    {
        var user = await _userRepository.GetUserByEmailAndTenantIdAsync(request.Email, request.TenantId);
        if (user == null)
        {
            throw new UserNotFoundException("User not found.");
        }
        if (user.IsEmailVerified)
        {
            throw new ArgumentException("Email already verified.");
        }
        var otp = GenerateVerificationOtp();
        var subject = "Verify your email";
        var body = $"Hello {user.FirstName},\n\nThanks for registering with SchoolMaster!\n\nPlease verify your email by using the code below: {otp}\n\nRegards,\n\nSchoolMaster Team";

        user.OtpToken = otp;
        user.OtpExpiry = DateTime.UtcNow.AddMinutes(_emailOptions.Value.ExpirationInMinutes);
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateUserAsync(user);

        _backgroundJobClient.Enqueue<IEmailService>(x =>
        x.SendEmailAsync(user.Email, user.FirstName, subject, body));

        return BaseResponse<bool>.SuccessResponse("Verification token resent successfully", true);
    }

    public async Task<BaseResponse<bool>> ForgotPasswordAsync(ForgetPasswordRequest request)
    {
        // 1. Get the TenantId automatically from our abstraction!
        var tenantId = _currentTenant.Id;
        if (tenantId == Guid.Empty)
        {
            Log.Error("Tenant not found for email {Email} in tenant {TenantId}", request.Email, tenantId);

            return BaseResponse<bool>.SuccessResponse("Forgot password token sent successfully", true);
        }
        var user = await _userRepository.GetUserByEmailAndTenantIdAsync(request.Email, tenantId);
        if (user == null)
        {
            Log.Error("User not found for email {Email} in tenant {TenantId}", request.Email, tenantId);
            return BaseResponse<bool>.SuccessResponse("Forgot password token sent successfully", true);
        }
        var otp = GenerateVerificationOtp();
        var subject = "Forgot your password";
        var body = $"Hello {user.FirstName},\n\nForgot your password? Use the code below to reset it:\n\n{otp}\n\nRegards,\n\nSchoolMaster Team";

        user.OtpToken = otp;
        user.OtpExpiry = DateTime.UtcNow.AddMinutes(_emailOptions.Value.ExpirationInMinutes);
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
            Log.Error("Tenant not found for email {Email} in tenant {TenantId}", request.Email, tenantId);

            throw new InvalidOtpException("Invalid OTP or Email address.");
        }
        var user = await _userRepository.GetUserByEmailAndTenantIdAsync(request.Email, _currentTenant.Id);
        if (user == null || user.OtpToken != request.Otp)
        {
            Log.Error("User not found for email {Email} in tenant {TenantId}", request.Email, tenantId);
            throw new InvalidOtpException("Invalid OTP or Email address.");
        }
        if (user.OtpExpiry < DateTime.UtcNow)
        {
            throw new OtpExpiredException("OTP has expired. Please request a new one.");
        }
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        user.OtpToken = null;
        user.OtpExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateUserAsync(user);
        return BaseResponse<bool>.SuccessResponse("Password reset successfully", true);
    }
}
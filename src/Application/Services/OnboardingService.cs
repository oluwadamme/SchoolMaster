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
    private readonly IOtpService _otpService;

    public OnboardingService(
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IOptions<EmailVerificationOptions> emailOptions,
        IBackgroundJobClient backgroundJobClient,
        ICurrentTenant currentTenant,
       IOtpService otpService)
    {
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _emailOptions = emailOptions;
        _backgroundJobClient = backgroundJobClient;
        _currentTenant = currentTenant;
        _otpService = otpService;

    }

    public async Task<BaseResponse<Guid>> CreateTenantWithAdminAsync(OnboardTenantRequest request)
    {
        // 1. Subdomain must be globally unique (it is the tenant's address).
        if (await _tenantRepository.ExistsBySubdomainAsync(request.Subdomain))
        {
            throw new AlreadyExistException("Subdomain already exists.");
        }

        // 2. Create Tenant
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.SchoolName,
            Subdomain = request.Subdomain,
            ContactEmail = request.ContactEmail,
            Status = TenantStatus.Active,
            Plan = TenantPlan.Basic,
            CreatedAt = DateTime.UtcNow,
            SchoolCode = request.SchoolCode,
            UpdatedAt = DateTime.UtcNow
        };

        // Email is unique per tenant, not globally. A brand-new tenant has no users yet, so this
        // correctly ALLOWS the same email to administer a different school while still guarding
        // against duplicates within this tenant.
        if (await _userRepository.ExistsByEmailInTenantAsync(request.AdminEmail, tenant.Id))
        {
            throw new AlreadyExistException("Admin email already exists in this school.");
        }

        await _tenantRepository.AddTenantAsync(tenant);

        var otp = _otpService.GenerateVerificationOtp();
        var subject = "Verify your email";
        var body = $"Hello {request.AdminFirstName},\n\nThanks for registering with SchoolMaster!\n\nPlease verify your email by using the code below: {otp}\n\nRegards,\n\nSchoolMaster Team";


        // 3. Create Admin User (linked to tenant)
        var adminUser = User.Create(
            tenantId: tenant.Id,
            status: UserStatus.PendingVerification,
            roles: new List<UserRole> { UserRole.Admin },
            firstName: request.AdminFirstName,
            lastName: request.AdminLastName,
            email: request.AdminEmail,
            passwordHash: BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
            otpToken: otp,
            otpExpiry: DateTime.UtcNow.AddMinutes(_emailOptions.Value.ExpirationInMinutes)
        );

        await _userRepository.AddUserAsync(adminUser);
        // 5. Return tenantId
        return BaseResponse<Guid>.SuccessResponse(
            "Tenant and Admin created successfully",
            tenant.Id
        );
    }

    public async Task<BaseResponse<bool>> VerifyUserEmailAsync(VerifyUserEmailRequest request)
    {
        var tenantId = _currentTenant.Id;
        if (tenantId == Guid.Empty)
        {
            Log.Error("Tenant not found for email {Email} in tenant {TenantId}", request.Email, tenantId);

            throw new InvalidOtpException("Invalid OTP or Email address.");
        }
        var user = await _userRepository.GetUserByEmailAsync(request.Email);
        if (user == null || user.OtpToken != request.OtpToken || user.OtpExpiry < DateTime.UtcNow)
        {
            throw new InvalidOtpException("Invalid OTP or email address.");
        }
        user.IsEmailVerified = true;
        user.Status = UserStatus.Active;
        user.OtpToken = null;
        user.OtpExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;
        user.Status = UserStatus.Active;
        await _userRepository.UpdateUserAsync(user);
        return BaseResponse<bool>.SuccessResponse("Email verified successfully", true);
    }

    public async Task<BaseResponse<bool>> ResendVerificationOtpAsync(ResendOtpRequest request)
    {
        var tenantId = _currentTenant.Id;
        if (tenantId == Guid.Empty)
        {
            Log.Error("Tenant not found for email {Email} in tenant {TenantId}", request.Email, tenantId);

            return BaseResponse<bool>.SuccessResponse("Otp sent successfully", true);
        }
        var user = await _userRepository.GetUserByEmailAsync(request.Email);
        if (user == null)
        {
            Log.Error("User not found for email {Email} in tenant {TenantId}", request.Email, tenantId);
            return BaseResponse<bool>.SuccessResponse("Otp sent successfully", true);
        }
        if (user.IsEmailVerified)
        {
            Log.Error("Email already verified for email {Email} in tenant {TenantId}", request.Email, tenantId);
            return BaseResponse<bool>.SuccessResponse("Email already verified", true);
        }
        var otp = _otpService.GenerateVerificationOtp();
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

}
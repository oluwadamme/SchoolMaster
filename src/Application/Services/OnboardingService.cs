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
    private readonly ICurrentTenant _currentTenant;
    private readonly IOtpService _otpService;
    private readonly IUnitOfWork _unitOfWork;

    public OnboardingService(
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IOptions<EmailVerificationOptions> emailOptions,
        ICurrentTenant currentTenant,
       IOtpService otpService,
       IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _emailOptions = emailOptions;
        _currentTenant = currentTenant;
        _otpService = otpService;
        _unitOfWork = unitOfWork;

    }

    public async Task<BaseResponse<OnboardTenantResponse>> CreateTenantWithAdminAsync(OnboardTenantRequest request)
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

        // 5. Return the created tenant plus the next step: the admin must verify their email
        //    with the OTP before the account becomes active.
        return BaseResponse<OnboardTenantResponse>.SuccessResponse(
            "Tenant and admin created successfully. A verification code has been sent to the admin email.",
            new OnboardTenantResponse(
                TenantId: tenant.Id,
                SchoolName: tenant.Name,
                Subdomain: tenant.Subdomain,
                AdminEmail: adminUser.Email,
                EmailVerificationRequired: true
            )
        );
    }

    public async Task<BaseResponse<bool>> VerifyUserEmailAsync(VerifyUserEmailRequest request)
    {
        var user = await _userRepository.GetUserByEmailAsync(request.Email);
        Log.Information("Verifying email {Email} with OTP {OtpToken}", user?.Email, request.OtpToken);
        if (user == null || user.OtpToken == null || user.OtpToken != request.OtpToken || user.OtpExpiry < DateTime.UtcNow)
        {
            // Count only genuine wrong guesses against a live OTP (not missing or expired) toward the
            // lockout, then wipe the OTP once the attempt budget is exhausted.
            if (user is { OtpToken: not null } && user.OtpExpiry >= DateTime.UtcNow && user.OtpToken != request.OtpToken)
            {
                user.RegisterFailedOtpAttempt();
                await _userRepository.UpdateUserAsync(user);
                // Commit the increment here: the throw below prevents UnitOfWorkFilter from committing
                // (it only saves when the action returns without an exception), so without this explicit
                // save every failed guess would be discarded and the attempt cap would never trigger.
                await _unitOfWork.SaveChangesAsync();
            }

            throw new InvalidOtpException("Invalid OTP or Email address.");
        }
        user.IsEmailVerified = true;
        user.Status = UserStatus.Active;
        user.ClearOtp();
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateUserAsync(user);
        return BaseResponse<bool>.SuccessResponse("Email verified successfully", true);
    }

    public async Task<BaseResponse<bool>> ResendVerificationOtpAsync(ResendOtpRequest request)
    {
        var tenantId = _currentTenant.Id;

        var user = await _userRepository.GetUserByEmailAsync(request.Email);
        if (user == null)
        {
            Log.Warning("Resend-OTP target not found in tenant {TenantId}.", tenantId);
            return BaseResponse<bool>.SuccessResponse("Otp sent successfully", true);
        }
        if (user.IsEmailVerified)
        {
            Log.Warning("Resend-OTP requested for an already-verified account in tenant {TenantId}.", tenantId);
            return BaseResponse<bool>.SuccessResponse("Email already verified", true);
        }
        var otp = _otpService.GenerateVerificationOtp();

        user.UpdateOtp(otp, DateTime.UtcNow.AddMinutes(_emailOptions.Value.ExpirationInMinutes));
        await _userRepository.UpdateUserAsync(user);

        return BaseResponse<bool>.SuccessResponse("Verification token resent successfully", true);
    }

}
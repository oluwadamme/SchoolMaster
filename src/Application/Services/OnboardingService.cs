using System;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using SchoolMaster.Application.DTOs;

using Microsoft.Extensions.Options;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Infrastructure.Options;
using Hangfire;
using System.Security.Cryptography;

namespace SchoolMaster.Application.Services;

public class OnboardingService : IOnboardingService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRepository _userRepository;
    private readonly IOptions<EmailVerificationOptions> _emailOptions;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public OnboardingService(
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IOptions<EmailVerificationOptions> emailOptions,
        IBackgroundJobClient backgroundJobClient)
    {
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _emailOptions = emailOptions;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<BaseResponse<Guid>> CreateTenantWithAdminAsync(OnboardTenantRequest request)
    {
        // 1. Check admin email uniqueness
        if (await _userRepository.ExistsByEmailAsync(request.AdminEmail))
        {
            throw new ArgumentException("Admin email already exists.");
        }

        if (await _tenantRepository.ExistsBySubdomainAsync(request.Subdomain))
        {
            throw new ArgumentException("Subdomain already exists.");
        }

        // 2. Create Tenant
        var tenant = new Tenant(Guid.NewGuid(), request.SchoolName, request.Subdomain, request.ContactEmail, TenantStatus.Active, TenantPlan.Basic, DateTime.UtcNow);

        await _tenantRepository.AddTenantAsync(tenant);

        var emailVerificationToken = GenerateVerificationToken();
        var subject = "Verify your email";
        var body = $"Hello {request.SchoolName},\n\nThanks for registering with SchoolMaster!\n\nPlease verify your email by using the code below: {emailVerificationToken}\n\nRegards,\n\nSchoolMaster Team";


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
            OtpToken = emailVerificationToken,
            OtpExpiry = DateTime.UtcNow.AddMinutes(_emailOptions.Value.ExpirationInMinutes),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepository.AddUserAsync(adminUser);

        // 4. Send email verification otp

        _backgroundJobClient.Enqueue<IEmailService>(x =>
        x.SendEmailAsync(request.AdminEmail, request.AdminFirstName, subject, body));

        // 4. Return tenantId
        return BaseResponse<Guid>.SuccessResponse(
            "Tenant and Admin created successfully",
            tenant.Id
        );
    }
    private string GenerateVerificationToken()
    {
        // generate 4 digit otp, if it is development env, the code will be 0000 else it will generate random code
        var token = RandomNumberGenerator.GetInt32(10000).ToString("D4");
        return token;
    }
}
namespace SchoolMaster.Application.Services;

using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.CustomException;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using System;
using System.Threading.Tasks;
using System.Transactions;
using Hangfire;
using Microsoft.Extensions.Options;
using SchoolMaster.Infrastructure.Options;

public class StaffService : IStaffService
{
    private readonly IUserRepository _userRepository;
    private readonly IStaffRepository _staffRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IOtpService _otpService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IOptions<EmailVerificationOptions> _emailOptions;

    public StaffService(
        IUserRepository userRepository, 
        IStaffRepository staffRepository, 
        ITenantRepository tenantRepository,
        ICurrentTenant currentTenant,
        IOtpService otpService,
        IBackgroundJobClient backgroundJobClient,
        IOptions<EmailVerificationOptions> emailOptions)
    {
        _userRepository = userRepository;
        _staffRepository = staffRepository;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _otpService = otpService;
        _backgroundJobClient = backgroundJobClient;
        _emailOptions = emailOptions;
    }

    public async Task<BaseResponse<StaffResponse>> CreateStaffAsync(CreateStaffRequest request)
    {
        var tenantId = _currentTenant.Id;

        if (tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Tenant ID not found for the current user.");
        }

        if (await _userRepository.ExistsByEmailAndTenantIdAsync(request.Email, tenantId))
        {
            throw new AlreadyExistException($"User with email '{request.Email}' already exists in this tenant.");
        }

        // Generate the unique Staff Number automatically
        var staffNumber = await GenerateStaffNumber(tenantId);

        var otp = _otpService.GenerateVerificationOtp();
        var otpExpiry = DateTime.UtcNow.AddDays(7); // Extended 7-day expiry for staff invitations

        // 1. Create the User (Object Initializer)
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Roles = new List<UserRole> { UserRole.Teacher },
            Status = UserStatus.PendingVerification,
            IsEmailVerified = false,
            OtpToken = otp,
            OtpExpiry = otpExpiry,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepository.AddUserAsync(user);

        // 2. Create the Staff (Object Initializer)
        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            StaffNumber = staffNumber,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Department = request.Department,
            StaffRole = request.StaffRole,
            EmploymentType = request.EmploymentType,
            Status = StaffStatus.Active,
            EmployedAt = DateTime.UtcNow
        };

        await _staffRepository.AddStaffAsync(staff);

        // Staff Invitation through email When account is created (database has staff info)
        var subject = "Action Required: Verify Your SchoolMaster Staff Account";
        var body = $"Hello {request.FirstName},\n\nYou have been added as a staff member. Please use the code below to verify your email and activate your account:\n\nVerification Code: {otp}\n\nThis invitation will expire in 7 days. Once verified, you can log in using the credentials provided by your administrator.";

        _backgroundJobClient.Enqueue<IEmailService>(x => x.SendEmailAsync(request.Email, request.FirstName, subject, body));

        var staffResponse = new StaffResponse(
            staff.Id, 
            staff.UserId, 
            staff.TenantId, 
            staff.StaffNumber, 
            staff.FirstName, 
            staff.LastName, 
            staff.Department, 
            staff.StaffRole, 
            staff.EmploymentType);

        return BaseResponse<StaffResponse>.SuccessResponse("Staff created successfully.", staffResponse);
    }

    public async Task<BaseResponse<bool>> ResendStaffInvitationAsync(ResendOtpRequest request)
    {
        var tenantId = _currentTenant.Id;

        // 1. Find the user in Susan's school
        var user = await _userRepository.GetUserByEmailAndTenantIdAsync(request.Email, tenantId);

        if (user == null)
        {
            // We return success even if not found to prevent "email fishing" 
            // but we log the attempt internally.
            return BaseResponse<bool>.SuccessResponse("If the account exists, a new invitation has been sent.", true);
        }

        if (user.IsEmailVerified)
        {
            return BaseResponse<bool>.SuccessResponse("This account is already verified.", true);
        }

        // 2. Generate a fresh code and a new 7-day window
        var otp = _otpService.GenerateVerificationOtp();
        user.OtpToken = otp;
        user.OtpExpiry = DateTime.UtcNow.AddDays(7);
        user.UpdatedAt = DateTime.UtcNow;

        // 3. Save the update
        await _userRepository.UpdateUserAsync(user);
        await _userRepository.SaveChangesAsync();

        // 4. Queue the new email
        var subject = "New Invitation: Verify Your SchoolMaster Staff Account";
        var body = $"Hello {user.FirstName},\n\nA new invitation code has been generated for you. Please use the code below to verify your email:\n\nVerification Code: {otp}\n\nThis code will expire in 7 days.";

        _backgroundJobClient.Enqueue<IEmailService>(x => 
            x.SendEmailAsync(user.Email, user.FirstName, subject, body));

        return BaseResponse<bool>.SuccessResponse("Invitation resent successfully.", true);
    }

    private async Task<string> GenerateStaffNumber(Guid tenantId)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId);
        if (tenant == null)
        {
            throw new TenantNotFoundException("School identification not found.");
        }

        var year = DateTime.UtcNow.Year;
        var prefix = $"{tenant.SchoolCode}/STF/{year}/";

        var lastCode = await _staffRepository.GetLastStaffNumberAsync(tenantId, prefix);

        int nextSequence = 1;
        if (lastCode != null)
        {
            var parts = lastCode.Split('/');
            // Expecting format: [CODE]/STF/[YEAR]/[SEQ] -> 4 parts
            if (parts.Length == 4 && int.TryParse(parts[3], out int lastSeq))
            {
                nextSequence = lastSeq + 1;
            }
        }

        return $"{prefix}{nextSequence:D6}";
    }
}
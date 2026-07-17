using static System.Guid;
namespace SchoolMaster.Application.Services;

using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.CustomException;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
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
    private readonly ITenantSequenceRepository _tenantSequenceRepository;
    private readonly IOptions<EmailVerificationOptions> _emailOptions;

    public StaffService(
        IUserRepository userRepository,
        IStaffRepository staffRepository,
        ITenantRepository tenantRepository,
        ICurrentTenant currentTenant,
        IOtpService otpService,
        ITenantSequenceRepository tenantSequenceRepository,
        IOptions<EmailVerificationOptions> emailOptions)
    {
        _userRepository = userRepository;
        _staffRepository = staffRepository;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _otpService = otpService;
        _tenantSequenceRepository = tenantSequenceRepository;
        _emailOptions = emailOptions;
    }

    public async Task<BaseResponse<StaffResponse>> UpdateStaffAsync(UpdateStaffRequest request)
    {
        var tenantId = _currentTenant.Id;

        // Assume IStaffRepository provides GetStaffByIdAsync
        var staff = await _staffRepository.GetStaffByIdAsync(request.StaffId);
        if (staff == null)
        {
            throw new KeyNotFoundException("Staff not found.");
        }

        // Fetch associated user
        var user = await _userRepository.GetUserByIdAsync(staff.UserId, tenantId);
        if (user == null)
        {
            throw new KeyNotFoundException("Associated user not found.");
        }

        if (request.Email.HasValue && request.Email.Value != null && request.Email.Value != user.Email)
        {
            // Ensure unique email within tenant
            if (await _userRepository.ExistsByEmailAndTenantIdAsync(request.Email.Value, tenantId))
            {
                throw new AlreadyExistException($"Email {request.Email.Value} is already registered.");
            }
            user.Email = request.Email.Value;
        }

        // Update mutable fields if provided
        if (request.FirstName.HasValue && request.FirstName.Value != null) { staff.FirstName = request.FirstName.Value; user.FirstName = request.FirstName.Value; }
        if (request.LastName.HasValue && request.LastName.Value != null) { staff.LastName = request.LastName.Value; user.LastName = request.LastName.Value; }
        if (request.Department.HasValue && request.Department.Value != null) staff.Department = request.Department.Value;
        if (request.StaffRole.HasValue) staff.StaffRole = request.StaffRole.Value;
        if (request.EmploymentType.HasValue) staff.EmploymentType = request.EmploymentType.Value;

        // Persist changes
        await _userRepository.UpdateUserAsync(user);

        var response = new StaffResponse(
            staff.Id,
            staff.UserId,
            staff.TenantId,
            staff.StaffNumber,
            staff.FirstName,
            staff.LastName,
            staff.Department,
            staff.StaffRole,
            staff.EmploymentType);

        return BaseResponse<StaffResponse>.SuccessResponse("Staff updated successfully.", response);
    }

    public async Task<BaseResponse<StaffResponse>> CreateStaffAsync(CreateStaffRequest request)
    {
        var tenantId = _currentTenant.Id;

        if (await _userRepository.ExistsByEmailAndTenantIdAsync(request.Email, tenantId))
        {
            throw new AlreadyExistException($"User with email '{request.Email}' already exists in this tenant.");
        }

        var tenant = await _tenantRepository.GetByIdAsync(tenantId);
        if (tenant == null) throw new TenantNotFoundException("School identification not found.");


        // Generate the unique Staff Number automatically
        var staffNumber = await GenerateStaffNumber(tenantId, tenant.SchoolCode, 1, 0);

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

    // ✅ Bulk enrollment for staff with partial success
    //  the code takes all 1,000 students and puts them into one big group in the computer's memory.
    // Then, it connects to the database exactly one time
    public async Task<BaseResponse<BulkEnrollmentResult>> EnrollStaffBulkAsync(BulkEnrollStaffRequest requests)
    {

        var tenantId = _currentTenant.Id;
        var requestList = requests.Staff.ToList();
        var tenant = await _tenantRepository.GetByIdAsync(tenantId);
        if (tenant == null) throw new TenantNotFoundException("School identification not found.");

        // 1. Make a list of request emails and fetch all emails that already exist in the database
        var emails = requestList.Select(x => x.Email.Trim().ToLowerInvariant()).Distinct().ToList();
        var existingEmails = await _userRepository.GetExistingEmailsAsync(emails, tenantId);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var accepted = new List<(int Row, CreateStaffRequest Req)>();
        var failures = new List<BulkEnrollmentFailure>();


        for (var i = 0; i < requestList.Count; i++)
        {
            var email = emails[i];
            if (existingEmails.Contains(email) || !seen.Add(email))
            {
                failures.Add(new BulkEnrollmentFailure(i + 1, requestList[i].Email, "Email already exists."));
                continue;
            }
            accepted.Add((i + 1, requestList[i]));
        }
        if (accepted.Count > 0)
        {
            var usersToInsert = new List<User>();
            var staffToInsert = new List<Staff>();
            for (var i = 0; i < accepted.Count; i++)
            {
                var req = accepted[i].Req;
                var otp = _otpService.GenerateVerificationOtp();
                var otpExpiry = DateTime.UtcNow.AddDays(7);

                var user = User.Create(
                    tenantId: tenantId,
                    status: UserStatus.PendingVerification,
                    roles: new List<UserRole> { UserRole.Teacher },
                    firstName: req.FirstName,
                    lastName: req.LastName,
                    email: req.Email,
                    passwordHash: BCrypt.Net.BCrypt.HashPassword(req.Password),
                    otpToken: otp,
                    otpExpiry: otpExpiry
                );
                var staffNumber = await GenerateStaffNumber(tenantId, tenant.SchoolCode, accepted.Count, i);

                usersToInsert.Add(user);
                var staff = new Staff
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    TenantId = tenantId,
                    StaffNumber = staffNumber,
                    FirstName = req.FirstName,
                    LastName = req.LastName,
                    Department = req.Department,
                    StaffRole = req.StaffRole,
                    EmploymentType = req.EmploymentType,
                    Status = StaffStatus.Active,
                    EmployedAt = DateTime.UtcNow
                };
                staffToInsert.Add(staff);
            }
            await _userRepository.AddUsersBulkAsync(usersToInsert);
            await _staffRepository.AddStaffBulkAsync(staffToInsert);
        }
        var results = new BulkEnrollmentResult
        (
            requestList.Count,
            accepted.Count,
            failures.Count,
            failures
        );

        return BaseResponse<BulkEnrollmentResult>.SuccessResponse(
            "Bulk staff enrollment completed.", results);
    }

    public async Task<BaseResponse<PagedResponse<StaffResponse>>> GetAllStaffAsync(int page, int pageSize)
    {
        var tenantId = _currentTenant.Id;
        
        var (staffList, totalCount) = await _staffRepository.GetAllStaffAsync(tenantId, page, pageSize);
        
        var responseList = staffList.Select(staff => new StaffResponse(
            staff.Id,
            staff.UserId,
            staff.TenantId,
            staff.StaffNumber,
            staff.FirstName,
            staff.LastName,
            staff.Department,
            staff.StaffRole,
            staff.EmploymentType
        )).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var pagedResponse = new PagedResponse<StaffResponse>(responseList, totalCount, totalPages, page, pageSize);
        return BaseResponse<PagedResponse<StaffResponse>>.SuccessResponse("Staff retrieved successfully.", pagedResponse);
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

        user.UpdateOtp(otp, DateTime.UtcNow.AddDays(7));
        // 3. Save the update
        await _userRepository.UpdateUserAsync(user);
        return BaseResponse<bool>.SuccessResponse("Invitation resent successfully.", true);
    }

    private async Task<string> GenerateStaffNumber(Guid tenantId, string schoolCode, int count = 1, int index = 0)
    {

        var year = DateTime.UtcNow.Year;
        var prefix = $"{schoolCode}/STF/{year}/";
        var blockStart = await _tenantSequenceRepository.ReserveBlockAsync(tenantId, "STAFF", year, count);


        return $"{prefix}{(blockStart + index):D6}";
    }
}
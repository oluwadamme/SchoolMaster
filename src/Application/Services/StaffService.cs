namespace SchoolMaster.Application.Services;

using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.CustomException;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using System;
using System.Threading.Tasks;

public class StaffService : IStaffService
{
    private readonly IUserRepository _userRepository;
    private readonly IStaffRepository _staffRepository;
    private readonly ICurrentTenant _currentTenant;


    public StaffService(IUserRepository userRepository, IStaffRepository staffRepository, ICurrentTenant currentTenant)
    {
        _userRepository = userRepository;
        _staffRepository = staffRepository;
        _currentTenant = currentTenant;    }

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

        if (await _staffRepository.ExistsByStaffNumberAsync(request.StaffNumber, tenantId))
        {
            throw new AlreadyExistException($"Staff with number '{request.StaffNumber}' already exists in this tenant.");
        }

        // 1. Create the User (Object Initializer)
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Teacher,
            Status = UserStatus.Active,
            IsEmailVerified = false,
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
            StaffNumber = request.StaffNumber,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Department = request.Department,
            StaffRole = request.StaffRole,
            EmploymentType = request.EmploymentType,
            Status = StaffStatus.Active,
            EmployedAt = DateTime.UtcNow
        };

        await _staffRepository.AddStaffAsync(staff);

        // Atomic save for both User and Staff
        await _staffRepository.SaveChangesAsync();

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
}
namespace SchoolMaster.Application.Services;

using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;


public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    // private readonly IUnitOfWork _unitOfWork;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<BaseResponse<Guid>> CreateAdminAsync(CreateAdminRequest request, Guid tenantId)
    {
        // 1. Check if email already exists
        if (await _userRepository.ExistsByEmailAsync(request.Email))
        {
            throw new ArgumentException("Email already exists");
        }

        // 2. Create User (ADMIN)
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Admin,
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow
        };

        // 3. Save
        await _userRepository.AddAsync(user);

        return BaseResponse<Guid>.SuccessResponse("Admin created successfully", user.Id);
    }
}

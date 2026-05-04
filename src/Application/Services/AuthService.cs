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
            return BaseResponse<Guid>.ErrorResponse("Email already exists");
        }

        // 2. Create User (ADMIN)
        var user = new User(
            Guid.NewGuid(),
            tenantId,
            request.Email,
            BCrypt.Net.BCrypt.HashPassword(request.Password),
            UserRole.Admin,
            true,
            null,
            null,
            null,
            null,
            DateTime.UtcNow
        );

        // 3. Save
        await _userRepository.AddAsync(user);

        return BaseResponse<Guid>.SuccessResponse("Admin created successfully", user.Id);
    }
}

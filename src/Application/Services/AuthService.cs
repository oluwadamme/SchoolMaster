using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.Services.Interfaces;

namespace SchoolMaster.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;

    public AuthService(IUserRepository userRepository, IJwtService jwtService)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
    }

    public async Task<BaseResponse<AuthResponse>> LoginAsync(LoginRequest request)
    {
        // 1. Find the user in the database using their email.
        // The IUserRepository tool helps us do this.
        var user = await _userRepository.GetUserByEmailAsync(request.Email);

        // If no user is found with that email, it means the email is wrong.
        if (user == null)
        {
            return BaseResponse<AuthResponse>.ErrorResponse("Invalid email or password.");
        }

        // 2. Check if the password is correct.
        // We use BCrypt to compare the typed password with the scrambled one (Hash) in the database.
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!isPasswordValid)
        {
            return BaseResponse<AuthResponse>.ErrorResponse("Invalid email or password.");
        }

        // 3. Create the Digital ID Badge (Token).
        var token = _jwtService.GenerateToken(user);

        // 4. Put everything into the Response container and send it back.
        var authResponse = new AuthResponse(
            token,
            user.FirstName,
            user.LastName,
            user.Role.ToString()
        );

        return BaseResponse<AuthResponse>.SuccessResponse("Login successful.", authResponse);
    }
}
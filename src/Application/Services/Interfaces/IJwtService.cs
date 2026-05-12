using SchoolMaster.Domain.Entities;

namespace SchoolMaster.Application.Services.Interfaces;

/// <summary>
/// This is the rule book for creating JWT (JSON Web Token) tokens.
/// It says that any service doing "JWT" must have a task to generate a token for a user.
/// </summary>
public interface IJwtService
{
    // This task takes a User and returns the generated JWT token as a string.
    string GenerateToken(User user);
}
using SchoolMaster.Domain.Entities;
using System.Security.Claims;

namespace SchoolMaster.Application.Services.Interfaces;

/// <summary>
/// This is the rule book for creating JWT (JSON Web Token) tokens.
/// It says that any service doing "JWT" must have a task to generate a token for a user.
/// </summary>
public interface IJwtService
{
    string GenerateAccessToken(User user);// short lived token. carries user info and permissions. Sent with every request to protected endpoints. Should be stored in memory on the client side.
    string GenerateRefreshToken(); // token sent to get a new access token when the old one expires. This is long lived and should be stored securely on the client side.
    ClaimsPrincipal GetPrincipalFromExpiredToken(string token); // This rule says: "Read the facts about the user from this dead token."
}

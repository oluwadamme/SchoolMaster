using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Infrastructure.Options;

namespace SchoolMaster.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly JwtOptions _jwtOptions;

    public JwtService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public string GenerateToken(User user)
    {
        // 1. Prepare the secret key.
        // This key is like a secret stamp only our server has.
        var key = Encoding.ASCII.GetBytes(_jwtOptions.Key);

        // 2. Create the "claims" (the information on the ID badge).
        // This includes the user's ID, email, first name, last name, and role.
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.GivenName, user.FirstName),
            new Claim(ClaimTypes.Surname, user.LastName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("tenant_id", user.TenantId.ToString()) // Important for multi-tenancy!
        };

        // 3. Describe the token (who made it, who it's for, how long it lasts).
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationInMinutes), // Token expires after this time.
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        // 4. Create and write the token.
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token); // This is the actual string (the ID badge).
    }
}
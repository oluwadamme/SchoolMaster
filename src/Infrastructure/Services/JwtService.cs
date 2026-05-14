using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
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

    public string GenerateAccessToken(User user)
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

    public string GenerateRefreshToken()
    {
        // 1. Create a very long, random sequence of bytes (numbers).
        var randomNumber = new byte[32]; // 32 bytes is a good length for a secure token.
        using var rng = RandomNumberGenerator.Create(); // This is a special tool to make truly random numbers.
        rng.GetBytes(randomNumber); // Fill the randomNumber array with random bytes.
        return Convert.ToBase64String(randomNumber); // Turn these random bytes into a safe text string.
    }

    public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        // 1. Set the rules for reading the token.
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false, // We don't need to check who the token was for yet.
            ValidateIssuer = false,   // We don't need to check who made the token yet.
            ValidateIssuerSigningKey = true, // We DO check if the secret key is correct.
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key)),
            ValidateLifetime = false // This is the most important part! 
                                     // We tell the computer to NOT care that the token is expired.
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        
        // 2. Try to read the token using the rules above.
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

        // 3. Check if the token was signed with the correct math (HMAC SHA256).
        var jwtSecurityToken = securityToken as JwtSecurityToken;
        if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new SecurityTokenException("Invalid token");
        }

        return principal;
    }
}
// Identity layer — auth only
namespace SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
public class User
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public bool IsEmailVerified { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public string? OtpToken { get; set; }
    public DateTime? OtpExpiry { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    // public User(Guid id, Guid tenantId, string email, string passwordHash, UserRole role, bool isEmailVerified, string? refreshToken, DateTime? refreshTokenExpiry, string? otpToken, DateTime? otpExpiry, DateTime createdAt)
    // {
    //     Id = id;
    //     TenantId = tenantId;
    //     Email = email;
    //     PasswordHash = passwordHash;
    //     Role = role;
    //     IsEmailVerified = isEmailVerified;
    //     RefreshToken = refreshToken;
    //     RefreshTokenExpiry = refreshTokenExpiry;
    //     OtpToken = otpToken;
    //     OtpExpiry = otpExpiry;
    //     CreatedAt = createdAt;
    // }
}

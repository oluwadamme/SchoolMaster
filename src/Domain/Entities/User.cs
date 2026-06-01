// Identity layer — auth only
namespace SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using System.Diagnostics.CodeAnalysis;

public class User
{
<<<<<<< HEAD
    public required Guid Id { get; set; }
    public required Guid TenantId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required UserRole Role { get; set; }

    public required UserStatus Status { get; set; }
=======
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public List<UserRole> Roles { get; set; } = new();

    public UserStatus Status { get; set; } = UserStatus.PendingVerification;
>>>>>>> f13bbbb66234ba5f86148debdd46d7224e35d8bd
    public bool IsEmailVerified { get; set; }
    
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public string? OtpToken { get; set; }
    public DateTime? OtpExpiry { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public void UpdateRefreshToken(string token, int daysToLive)
    {
        RefreshToken = token;
        RefreshTokenExpiry = DateTime.UtcNow.AddDays(daysToLive);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        Status = UserStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
    }
}

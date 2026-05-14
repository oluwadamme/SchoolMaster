// Identity layer — auth only
namespace SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
public class User
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public UserRole Role { get; set; }

    public UserStatus Status { get; private set; } = UserStatus.Active;
    public bool IsEmailVerified { get; set; }
    public string? RefreshToken { get; private set; } // only code inside the user class can change it. Other parts of the program would have to User class methods to update it
    public DateTime? RefreshTokenExpiry { get; private set; } //store data and time when refresh token will expire
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

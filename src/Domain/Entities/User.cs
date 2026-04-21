// Identity layer — auth only
namespace SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
public class User
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsEmailVerified { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiry { get; private set; }
    public string? OtpToken { get; private set; }
    public DateTime? OtpExpiry { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

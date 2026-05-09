// Identity layer — auth only
namespace SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
public class User
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsEmailVerified { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiry { get; private set; }
    public string? OtpToken { get; private set; }
    public DateTime? OtpExpiry { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public User(
    Guid id,
    Guid tenantId,
    string firstName,
    string lastName,
    string email,
    string passwordHash,
    UserRole role,
    bool isEmailVerified,
    DateTime createdAt)
    {
        Id = id;
        TenantId = tenantId;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        IsEmailVerified = isEmailVerified;
        CreatedAt = createdAt;
    }
}

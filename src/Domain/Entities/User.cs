// Identity layer — auth only
using SchoolMaster.Domain.Common;
namespace SchoolMaster.Domain.Entities;

using SchoolMaster.Domain.Enums;
using SchoolMaster.Domain.Events;

public class User : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new(); public required Guid Id { get; set; }
    public required Guid TenantId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public List<UserRole> Roles { get; set; } = new();

    public UserStatus Status { get; set; } = UserStatus.PendingVerification;
    public bool IsEmailVerified { get; set; }
    
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public string? OtpToken { get; set; }
    public DateTime? OtpExpiry { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();


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

    public static User Create(Guid tenantId, UserStatus status, List<UserRole> roles, string firstName, string lastName, string email, string passwordHash, string? otpToken = null, DateTime? otpExpiry = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PasswordHash = passwordHash,
            Status = status,
            IsEmailVerified = false,
            Roles = roles,
            OtpToken = otpToken,
            OtpExpiry = otpExpiry,
            CreatedAt = DateTime.UtcNow
        };

        if (status == UserStatus.PendingVerification && otpToken != null)
        {
            user._domainEvents.Add(new OtpVerificationEvent(tenantId, email, firstName, otpToken));
        }

        return user;
    }

    public void UpdateOtp(string otpToken, DateTime otpExpiry)
    {
        OtpToken = otpToken;
        OtpExpiry = otpExpiry;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new OtpVerificationEvent(TenantId, Email, FirstName, otpToken));
    }
}

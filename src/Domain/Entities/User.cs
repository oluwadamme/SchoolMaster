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

    // Number of consecutive wrong OTP guesses for the current OTP. Reset whenever a new OTP is
    // issued or an OTP is consumed. Used to invalidate an OTP after too many guesses (anti brute force).
    public int OtpAttemptCount { get; set; }

    // Rotated whenever a session must be invalidated (password reset, deactivation). The value is
    // embedded in every access token and re-checked on each authenticated request, so changing it
    // immediately invalidates all previously issued access tokens for this user.
    public Guid SecurityStamp { get; private set; } = Guid.NewGuid();

    // Consecutive failed login attempts and the time a temporary lockout ends. Protects against
    // password brute force at the account level (complements the per-IP rate limiter).
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockoutEndUtc { get; private set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Lockout policy. Kept on the entity so the rule lives with the data it guards.
    public const int MaxFailedLoginAttempts = 5;
    public const int LockoutMinutes = 15;
    public const int MaxOtpAttempts = 5;
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();


    public void UpdateRefreshToken(string token, int daysToLive)
    {
        RefreshToken = token;
        RefreshTokenExpiry = DateTime.UtcNow.AddDays(daysToLive);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiry = null;
        UpdatedAt = DateTime.UtcNow;
    }

    // Invalidates every access token already issued for this user.
    public void RotateSecurityStamp()
    {
        SecurityStamp = Guid.NewGuid();
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsLockedOut() => LockoutEndUtc > DateTime.UtcNow;

    public void RegisterFailedLogin()
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= MaxFailedLoginAttempts)
        {
            LockoutEndUtc = DateTime.UtcNow.AddMinutes(LockoutMinutes);
            FailedLoginAttempts = 0;
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginAttempts = 0;
        LockoutEndUtc = null;
        LastLoginAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearOtp()
    {
        OtpToken = null;
        OtpExpiry = null;
        OtpAttemptCount = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    // Records a wrong OTP guess. Returns true if this guess exhausted the allowed attempts, in which
    // case the OTP is wiped and the caller must request a fresh one.
    public bool RegisterFailedOtpAttempt()
    {
        OtpAttemptCount++;
        UpdatedAt = DateTime.UtcNow;
        if (OtpAttemptCount >= MaxOtpAttempts)
        {
            ClearOtp();
            return true;
        }
        return false;
    }

    public void Deactivate()
    {
        Status = UserStatus.Inactive;
        // Kill existing sessions so deactivation takes effect immediately, not after token expiry.
        RotateSecurityStamp();
        ClearRefreshToken();
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
        OtpAttemptCount = 0;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new OtpVerificationEvent(TenantId, Email, FirstName, otpToken));
    }
}

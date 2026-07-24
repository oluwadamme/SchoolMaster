using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using Xunit;

namespace SchoolMaster.Tests.Unit.Entities;

public class UserTests
{
    private static User MakeUser() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        FirstName = "A",
        LastName = "B",
        Email = "a@test.com",
        PasswordHash = "h",
        Roles = [UserRole.Admin],
    };

    [Fact]
    public void Deactivate_SetsInactiveRotatesStampAndClearsRefreshToken()
    {
        var user = MakeUser();
        user.UpdateRefreshToken("active-refresh", 7);
        var originalStamp = user.SecurityStamp;

        user.Deactivate();

        Assert.Equal(UserStatus.Inactive, user.Status);
        Assert.Null(user.RefreshToken);
        Assert.Null(user.RefreshTokenExpiry);
        Assert.NotEqual(originalStamp, user.SecurityStamp);
    }

    [Fact]
    public void RegisterFailedLogin_LocksOutAfterMaxAttempts()
    {
        var user = MakeUser();

        for (var i = 0; i < User.MaxFailedLoginAttempts - 1; i++)
        {
            user.RegisterFailedLogin();
            Assert.False(user.IsLockedOut());
        }

        user.RegisterFailedLogin(); // the attempt that crosses the threshold
        Assert.True(user.IsLockedOut());
    }

    [Fact]
    public void RegisterSuccessfulLogin_ClearsLockoutState()
    {
        var user = MakeUser();
        for (var i = 0; i < User.MaxFailedLoginAttempts; i++) user.RegisterFailedLogin();
        Assert.True(user.IsLockedOut());

        user.RegisterSuccessfulLogin();

        Assert.False(user.IsLockedOut());
        Assert.Equal(0, user.FailedLoginAttempts);
    }

    [Fact]
    public void RegisterFailedOtpAttempt_WipesOtpAfterMaxAttempts()
    {
        var user = MakeUser();
        user.OtpToken = "123456";
        user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);

        bool wiped = false;
        for (var i = 0; i < User.MaxOtpAttempts; i++)
            wiped = user.RegisterFailedOtpAttempt();

        Assert.True(wiped);
        Assert.Null(user.OtpToken);
        Assert.Null(user.OtpExpiry);
    }
}

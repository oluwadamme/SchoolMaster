using System.Security.Claims;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Options;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.Services;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.CustomException;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using SchoolMaster.Infrastructure.Options;
using Xunit;
using Moq;  

namespace SchoolMaster.Tests.Unit.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IJwtService> _jwtService = new();
    private readonly Mock<IBackgroundJobClient> _backgroundJobClient = new();
    private readonly Mock<ICurrentTenant> _currentTenant = new();
    private readonly Mock<IOtpService> _otpService = new();

    private readonly IOptions<EmailVerificationOptions> _emailOptions =
        Options.Create(new EmailVerificationOptions { ExpirationInMinutes = 15 });

    private AuthService CreateSut() => new(
        _userRepo.Object,
        _jwtService.Object,
        _backgroundJobClient.Object,
        _emailOptions,
        _currentTenant.Object,
        _otpService.Object);

    // Creates a user with a BCrypt-hashed password so LoginAsync can verify it.
    private static User MakeActiveUser(string email = "user@test.com", string password = "Test@123!")
    {
        return new User
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Doe",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Roles = [UserRole.Admin],
            IsEmailVerified = true,
        };
    }

    private static ClaimsPrincipal MakePrincipal(Guid userId, Guid tenantId) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("tenant_id", tenantId.ToString())
        }));

    // -------------------------------------------------------------------------
    // LoginAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsSuccessResponse()
    {
        const string password = "Test@123!";
        var user = MakeActiveUser(password: password);
        _userRepo.Setup(r => r.GetUserByEmailAsync(user.Email)).ReturnsAsync(user);
        _jwtService.Setup(j => j.GenerateAccessToken(user)).Returns("access-token");
        _jwtService.Setup(j => j.GenerateRefreshToken()).Returns("refresh-token");

        var result = await CreateSut().LoginAsync(new LoginRequest(user.Email, password));

        Assert.True(result.Success);
        Assert.Equal("Login successful.", result.Message);
        Assert.Equal("access-token", result.Data!.Token);
        Assert.Equal("refresh-token", result.Data.RefreshToken);
        Assert.Equal(user.Id, result.Data.Id);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ThrowsUserNotFoundException()
    {
        _userRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UserNotFoundException>(
            () => CreateSut().LoginAsync(new LoginRequest("unknown@test.com", "any")));
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsInvalidCredentialsException()
    {
        var user = MakeActiveUser(password: "CorrectPass@1");
        _userRepo.Setup(r => r.GetUserByEmailAsync(user.Email)).ReturnsAsync(user);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => CreateSut().LoginAsync(new LoginRequest(user.Email, "WrongPass@1")));
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_PersistsNewRefreshToken()
    {
        const string password = "Test@123!";
        var user = MakeActiveUser(password: password);
        _userRepo.Setup(r => r.GetUserByEmailAsync(user.Email)).ReturnsAsync(user);
        _jwtService.Setup(j => j.GenerateRefreshToken()).Returns("new-refresh");

        await CreateSut().LoginAsync(new LoginRequest(user.Email, password));

        _userRepo.Verify(
            r => r.UpdateUserAsync(It.Is<User>(u => u.RefreshToken == "new-refresh")),
            Times.Once);
    }

    // -------------------------------------------------------------------------
    // RefreshTokenAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RefreshTokenAsync_WithValidTokens_ReturnsNewTokenPair()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = userId, TenantId = tenantId, FirstName = "A", LastName = "B",
            Email = "a@b.com", PasswordHash = "x", Roles = [UserRole.Admin],
        };
        user.UpdateRefreshToken("valid-refresh", 7);

        _jwtService.Setup(j => j.GetPrincipalFromExpiredToken("old-access"))
            .Returns(MakePrincipal(userId, tenantId));
        _userRepo.Setup(r => r.GetUserByIdAsync(userId, tenantId)).ReturnsAsync(user);
        _jwtService.Setup(j => j.GenerateAccessToken(user)).Returns("new-access");
        _jwtService.Setup(j => j.GenerateRefreshToken()).Returns("new-refresh");

        var result = await CreateSut().RefreshTokenAsync(new RefreshTokenRequest("old-access", "valid-refresh"));

        Assert.True(result.Success);
        Assert.Equal("new-access", result.Data!.Token);
        Assert.Equal("new-refresh", result.Data.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenUserIdClaimMissing_ThrowsInvalidCredentialsException()
    {
        // Principal contains only tenant_id, no NameIdentifier
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(new[] { new Claim("tenant_id", Guid.NewGuid().ToString()) }));
        _jwtService.Setup(j => j.GetPrincipalFromExpiredToken(It.IsAny<string>())).Returns(principal);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => CreateSut().RefreshTokenAsync(new RefreshTokenRequest("access", "refresh")));
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTenantIdClaimMissing_ThrowsInvalidCredentialsException()
    {
        // Principal contains only NameIdentifier, no tenant_id
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) }));
        _jwtService.Setup(j => j.GetPrincipalFromExpiredToken(It.IsAny<string>())).Returns(principal);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => CreateSut().RefreshTokenAsync(new RefreshTokenRequest("access", "refresh")));
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenUserNotFound_ThrowsUserNotFoundException()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _jwtService.Setup(j => j.GetPrincipalFromExpiredToken(It.IsAny<string>()))
            .Returns(MakePrincipal(userId, tenantId));
        _userRepo.Setup(r => r.GetUserByIdAsync(userId, tenantId)).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UserNotFoundException>(
            () => CreateSut().RefreshTokenAsync(new RefreshTokenRequest("access", "refresh")));
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenRefreshTokenMismatch_ThrowsInvalidCredentialsException()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = userId, TenantId = tenantId, FirstName = "A", LastName = "B",
            Email = "a@b.com", PasswordHash = "x", Roles = [UserRole.Admin],
        };
        user.UpdateRefreshToken("stored-token", 7);

        _jwtService.Setup(j => j.GetPrincipalFromExpiredToken(It.IsAny<string>()))
            .Returns(MakePrincipal(userId, tenantId));
        _userRepo.Setup(r => r.GetUserByIdAsync(userId, tenantId)).ReturnsAsync(user);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => CreateSut().RefreshTokenAsync(new RefreshTokenRequest("access", "different-token")));
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenRefreshTokenExpired_ThrowsInvalidCredentialsException()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = userId, TenantId = tenantId, FirstName = "A", LastName = "B",
            Email = "a@b.com", PasswordHash = "x", Roles = [UserRole.Admin],
        };
        // AddDays(-1) → expiry is yesterday
        user.UpdateRefreshToken("expired-token", -1);

        _jwtService.Setup(j => j.GetPrincipalFromExpiredToken(It.IsAny<string>()))
            .Returns(MakePrincipal(userId, tenantId));
        _userRepo.Setup(r => r.GetUserByIdAsync(userId, tenantId)).ReturnsAsync(user);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => CreateSut().RefreshTokenAsync(new RefreshTokenRequest("access", "expired-token")));
    }

    // -------------------------------------------------------------------------
    // DeactivateUserByEmailAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeactivateUserByEmailAsync_WithExistingActiveUser_SetsStatusInactiveAndReturnsTrue()
    {
        var tenantId = Guid.NewGuid();
        var user = MakeActiveUser();
        _userRepo.Setup(r => r.GetUserByEmailAndTenantIdAsync(user.Email, tenantId)).ReturnsAsync(user);

        var result = await CreateSut().DeactivateUserByEmailAsync(user.Email, tenantId);

        Assert.True(result.Success);
        Assert.True(result.Data);
        _userRepo.Verify(
            r => r.UpdateUserAsync(It.Is<User>(u => u.Status == UserStatus.Inactive)),
            Times.Once);
    }

    [Fact]
    public async Task DeactivateUserByEmailAsync_WithUnknownEmail_ThrowsUserNotFoundException()
    {
        var tenantId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetUserByEmailAndTenantIdAsync(It.IsAny<string>(), tenantId))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UserNotFoundException>(
            () => CreateSut().DeactivateUserByEmailAsync("ghost@test.com", tenantId));
    }

    // -------------------------------------------------------------------------
    // ForgotPasswordAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ForgotPasswordAsync_WithEmptyTenantId_ReturnsSilentSuccessWithoutLookingUpUser()
    {
        _currentTenant.SetupGet(t => t.Id).Returns(Guid.Empty);

        var result = await CreateSut().ForgotPasswordAsync(new ForgetPasswordRequest { Email = "a@b.com" });

        Assert.True(result.Success);
        _userRepo.Verify(r => r.GetUserByEmailAndTenantIdAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WithUnknownEmail_ReturnsSilentSuccessWithoutSendingEmail()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.SetupGet(t => t.Id).Returns(tenantId);
        _userRepo.Setup(r => r.GetUserByEmailAndTenantIdAsync(It.IsAny<string>(), tenantId))
            .ReturnsAsync((User?)null);

        var result = await CreateSut().ForgotPasswordAsync(new ForgetPasswordRequest { Email = "ghost@test.com" });

        Assert.True(result.Success);
        _backgroundJobClient.Verify(b => b.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WithValidUser_StoresOtpOnUserAndEnqueuesEmail()
    {
        var tenantId = Guid.NewGuid();
        var user = MakeActiveUser();
        _currentTenant.SetupGet(t => t.Id).Returns(tenantId);
        _userRepo.Setup(r => r.GetUserByEmailAndTenantIdAsync(user.Email, tenantId)).ReturnsAsync(user);
        _otpService.Setup(o => o.GenerateVerificationOtp()).Returns("5678");

        await CreateSut().ForgotPasswordAsync(new ForgetPasswordRequest { Email = user.Email });

        Assert.Equal("5678", user.OtpToken);
        Assert.NotNull(user.OtpExpiry);
        _userRepo.Verify(r => r.UpdateUserAsync(user), Times.Once);
        _backgroundJobClient.Verify(b => b.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // ResetPasswordAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ResetPasswordAsync_WithValidOtp_HashesNewPasswordAndClearsOtpFields()
    {
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "A", LastName = "B",
            Email = "a@test.com", PasswordHash = "old-hash", Roles = [UserRole.Admin],
            OtpToken = "1234", OtpExpiry = DateTime.UtcNow.AddMinutes(10),
        };
        _currentTenant.SetupGet(t => t.Id).Returns(tenantId);
        _userRepo.Setup(r => r.GetUserByEmailAndTenantIdAsync(user.Email, tenantId)).ReturnsAsync(user);

        var result = await CreateSut().ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = user.Email, Password = "NewPass@1", Otp = "1234"
        });

        Assert.True(result.Success);
        Assert.Null(user.OtpToken);
        Assert.Null(user.OtpExpiry);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPass@1", user.PasswordHash));
    }

    [Fact]
    public async Task ResetPasswordAsync_WithEmptyTenantId_ThrowsInvalidOtpException()
    {
        _currentTenant.SetupGet(t => t.Id).Returns(Guid.Empty);

        await Assert.ThrowsAsync<InvalidOtpException>(
            () => CreateSut().ResetPasswordAsync(new ResetPasswordRequest
            {
                Email = "a@b.com", Password = "P@ss1A", Otp = "1234"
            }));
    }

    [Fact]
    public async Task ResetPasswordAsync_WithUnknownEmail_ThrowsInvalidOtpException()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.SetupGet(t => t.Id).Returns(tenantId);
        _userRepo.Setup(r => r.GetUserByEmailAndTenantIdAsync(It.IsAny<string>(), tenantId))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<InvalidOtpException>(
            () => CreateSut().ResetPasswordAsync(new ResetPasswordRequest
            {
                Email = "ghost@test.com", Password = "P@ss1A", Otp = "1234"
            }));
    }

    [Fact]
    public async Task ResetPasswordAsync_WithWrongOtp_ThrowsInvalidOtpException()
    {
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "A", LastName = "B",
            Email = "a@test.com", PasswordHash = "h", Roles = [UserRole.Admin],
            OtpToken = "correct", OtpExpiry = DateTime.UtcNow.AddMinutes(10),
        };
        _currentTenant.SetupGet(t => t.Id).Returns(tenantId);
        _userRepo.Setup(r => r.GetUserByEmailAndTenantIdAsync(user.Email, tenantId)).ReturnsAsync(user);

        await Assert.ThrowsAsync<InvalidOtpException>(
            () => CreateSut().ResetPasswordAsync(new ResetPasswordRequest
            {
                Email = user.Email, Password = "P@ss1A", Otp = "wrong"
            }));
    }

    [Fact]
    public async Task ResetPasswordAsync_WithExpiredOtp_ThrowsOtpExpiredException()
    {
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "A", LastName = "B",
            Email = "a@test.com", PasswordHash = "h", Roles = [UserRole.Admin],
            OtpToken = "1234", OtpExpiry = DateTime.UtcNow.AddMinutes(-5), // expired
        };
        _currentTenant.SetupGet(t => t.Id).Returns(tenantId);
        _userRepo.Setup(r => r.GetUserByEmailAndTenantIdAsync(user.Email, tenantId)).ReturnsAsync(user);

        await Assert.ThrowsAsync<OtpExpiredException>(
            () => CreateSut().ResetPasswordAsync(new ResetPasswordRequest
            {
                Email = user.Email, Password = "P@ss1A", Otp = "1234"
            }));
    }
}

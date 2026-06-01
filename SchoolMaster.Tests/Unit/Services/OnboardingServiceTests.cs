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

public class OnboardingServiceTests
{
    private readonly Mock<ITenantRepository> _tenantRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IBackgroundJobClient> _backgroundJobClient = new();
    private readonly Mock<ICurrentTenant> _currentTenant = new();
    private readonly Mock<IOtpService> _otpService = new();

    private readonly IOptions<EmailVerificationOptions> _emailOptions =
        Options.Create(new EmailVerificationOptions { ExpirationInMinutes = 15 });

    private OnboardingService CreateSut() => new(
        _tenantRepo.Object,
        _userRepo.Object,
        _emailOptions,
        _backgroundJobClient.Object,
        _currentTenant.Object,
        _otpService.Object);

    private static OnboardTenantRequest MakeValidRequest(string? email = null, string? subdomain = null) => new()
    {
        SchoolName = "Test School",
        Subdomain = subdomain ?? "test-school",
        ContactEmail = email ?? "contact@test.com",
        AdminFirstName = "Admin",
        AdminLastName = "User",
        AdminEmail = email ?? "admin@test.com",
        AdminPassword = "Test@123!",
    };

    // -------------------------------------------------------------------------
    // CreateTenantWithAdminAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateTenantWithAdminAsync_WithValidRequest_ReturnsTenantIdInSuccessResponse()
    {
        _userRepo.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>())).ReturnsAsync(false);
        _tenantRepo.Setup(r => r.ExistsBySubdomainAsync(It.IsAny<string>())).ReturnsAsync(false);
        _otpService.Setup(o => o.GenerateVerificationOtp()).Returns("1234");

        var result = await CreateSut().CreateTenantWithAdminAsync(MakeValidRequest());

        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.Data);
    }

    [Fact]
    public async Task CreateTenantWithAdminAsync_WithValidRequest_CreatesTenantAndUnverifiedAdmin()
    {
        _userRepo.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>())).ReturnsAsync(false);
        _tenantRepo.Setup(r => r.ExistsBySubdomainAsync(It.IsAny<string>())).ReturnsAsync(false);
        _otpService.Setup(o => o.GenerateVerificationOtp()).Returns("1234");

        await CreateSut().CreateTenantWithAdminAsync(MakeValidRequest());

        _tenantRepo.Verify(r => r.AddTenantAsync(It.Is<Tenant>(t =>
            t.Status == TenantStatus.Active && t.Plan == TenantPlan.Basic)), Times.Once);

        _userRepo.Verify(r => r.AddUserAsync(It.Is<User>(u =>
            u.Roles.Contains(UserRole.Admin) && !u.IsEmailVerified && u.OtpToken == "1234")), Times.Once);
    }

    [Fact]
    public async Task CreateTenantWithAdminAsync_WithValidRequest_EnqueuesVerificationEmail()
    {
        _userRepo.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>())).ReturnsAsync(false);
        _tenantRepo.Setup(r => r.ExistsBySubdomainAsync(It.IsAny<string>())).ReturnsAsync(false);
        _otpService.Setup(o => o.GenerateVerificationOtp()).Returns("1234");

        await CreateSut().CreateTenantWithAdminAsync(MakeValidRequest());

        _backgroundJobClient.Verify(b => b.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
    }

    [Fact]
    public async Task CreateTenantWithAdminAsync_WhenAdminEmailAlreadyExists_ThrowsAlreadyExistException()
    {
        _userRepo.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<AlreadyExistException>(
            () => CreateSut().CreateTenantWithAdminAsync(MakeValidRequest()));

        // Tenant should NOT have been created
        _tenantRepo.Verify(r => r.AddTenantAsync(It.IsAny<Tenant>()), Times.Never);
    }

    [Fact]
    public async Task CreateTenantWithAdminAsync_WhenSubdomainAlreadyExists_ThrowsAlreadyExistException()
    {
        _userRepo.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>())).ReturnsAsync(false);
        _tenantRepo.Setup(r => r.ExistsBySubdomainAsync(It.IsAny<string>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<AlreadyExistException>(
            () => CreateSut().CreateTenantWithAdminAsync(MakeValidRequest()));

        _userRepo.Verify(r => r.AddUserAsync(It.IsAny<User>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // VerifyUserEmailAsync
    // Note: this method combines null-user, wrong-OTP, and expired-OTP into a
    // single condition, so all three throw InvalidOtpException (not OtpExpiredException).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task VerifyUserEmailAsync_WithValidOtp_SetsEmailVerifiedAndClearsOtpFields()
    {
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "A", LastName = "B",
            Email = "a@test.com", PasswordHash = "h", Roles = [UserRole.Admin],
            OtpToken = "1234", OtpExpiry = DateTime.UtcNow.AddMinutes(15),
            IsEmailVerified = false,
        };
        _currentTenant.SetupGet(t => t.Id).Returns(tenantId);
        _userRepo.Setup(r => r.GetUserByEmailAndTenantIdAsync(user.Email, tenantId)).ReturnsAsync(user);

        var result = await CreateSut().VerifyUserEmailAsync(new VerifyUserEmailRequest
        {
            Email = user.Email, OtpToken = "1234"
        });

        Assert.True(result.Success);
        Assert.True(user.IsEmailVerified);
        Assert.Null(user.OtpToken);
        Assert.Null(user.OtpExpiry);
        _userRepo.Verify(r => r.UpdateUserAsync(user), Times.Once);
    }

    [Fact]
    public async Task VerifyUserEmailAsync_WithEmptyTenantId_ThrowsInvalidOtpException()
    {
        _currentTenant.SetupGet(t => t.Id).Returns(Guid.Empty);

        await Assert.ThrowsAsync<InvalidOtpException>(
            () => CreateSut().VerifyUserEmailAsync(new VerifyUserEmailRequest
            {
                Email = "a@b.com", OtpToken = "1234"
            }));
    }

    [Fact]
    public async Task VerifyUserEmailAsync_WithUnknownEmail_ThrowsInvalidOtpException()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.SetupGet(t => t.Id).Returns(tenantId);
        _userRepo.Setup(r => r.GetUserByEmailAndTenantIdAsync(It.IsAny<string>(), tenantId))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<InvalidOtpException>(
            () => CreateSut().VerifyUserEmailAsync(new VerifyUserEmailRequest
            {
                Email = "ghost@test.com", OtpToken = "1234"
            }));
    }

    [Fact]
    public async Task VerifyUserEmailAsync_WithWrongOtp_ThrowsInvalidOtpException()
    {
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "A", LastName = "B",
            Email = "a@test.com", PasswordHash = "h", Roles = [UserRole.Admin],
            OtpToken = "correct", OtpExpiry = DateTime.UtcNow.AddMinutes(15),
        };
        _currentTenant.SetupGet(t => t.Id).Returns(tenantId);
        _userRepo.Setup(r => r.GetUserByEmailAndTenantIdAsync(user.Email, tenantId)).ReturnsAsync(user);

        await Assert.ThrowsAsync<InvalidOtpException>(
            () => CreateSut().VerifyUserEmailAsync(new VerifyUserEmailRequest
            {
                Email = user.Email, OtpToken = "wrong"
            }));
    }

    [Fact]
    public async Task VerifyUserEmailAsync_WithExpiredOtp_ThrowsInvalidOtpException()
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

        // VerifyUserEmailAsync bundles all failure modes in one if-statement,
        // so expired OTP produces InvalidOtpException, not OtpExpiredException.
        await Assert.ThrowsAsync<InvalidOtpException>(
            () => CreateSut().VerifyUserEmailAsync(new VerifyUserEmailRequest
            {
                Email = user.Email, OtpToken = "1234"
            }));
    }

    // -------------------------------------------------------------------------
    // ResendVerificationOtpAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ResendVerificationOtpAsync_WithUnverifiedUser_StoresNewOtpAndEnqueuesEmail()
    {
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "A", LastName = "B",
            Email = "a@test.com", PasswordHash = "h", Roles = [UserRole.Admin],
            IsEmailVerified = false, OtpToken = "old-otp",
        };
        _currentTenant.SetupGet(t => t.Id).Returns(tenantId);
        _userRepo.Setup(r => r.GetUserByEmailAndTenantIdAsync(user.Email, tenantId)).ReturnsAsync(user);
        _otpService.Setup(o => o.GenerateVerificationOtp()).Returns("new-otp");

        var result = await CreateSut().ResendVerificationOtpAsync(new ResendOtpRequest { Email = user.Email });

        Assert.True(result.Success);
        Assert.Equal("new-otp", user.OtpToken);
        _userRepo.Verify(r => r.UpdateUserAsync(user), Times.Once);
        _backgroundJobClient.Verify(b => b.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
    }

    [Fact]
    public async Task ResendVerificationOtpAsync_WithEmptyTenantId_ReturnsSilentSuccessWithoutDbLookup()
    {
        _currentTenant.SetupGet(t => t.Id).Returns(Guid.Empty);

        var result = await CreateSut().ResendVerificationOtpAsync(new ResendOtpRequest { Email = "a@b.com" });

        Assert.True(result.Success);
        _userRepo.Verify(r => r.GetUserByEmailAndTenantIdAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ResendVerificationOtpAsync_WithUnknownEmail_ReturnsSilentSuccessWithoutSendingEmail()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.SetupGet(t => t.Id).Returns(tenantId);
        _userRepo.Setup(r => r.GetUserByEmailAndTenantIdAsync(It.IsAny<string>(), tenantId))
            .ReturnsAsync((User?)null);

        var result = await CreateSut().ResendVerificationOtpAsync(new ResendOtpRequest { Email = "ghost@test.com" });

        Assert.True(result.Success);
        _backgroundJobClient.Verify(b => b.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Never);
    }

    [Fact]
    public async Task ResendVerificationOtpAsync_WithAlreadyVerifiedUser_ReturnsEarlyWithoutSendingNewOtp()
    {
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "A", LastName = "B",
            Email = "a@test.com", PasswordHash = "h", Roles = [UserRole.Admin],
            IsEmailVerified = true,
        };
        _currentTenant.SetupGet(t => t.Id).Returns(tenantId);
        _userRepo.Setup(r => r.GetUserByEmailAndTenantIdAsync(user.Email, tenantId)).ReturnsAsync(user);

        var result = await CreateSut().ResendVerificationOtpAsync(new ResendOtpRequest { Email = user.Email });

        Assert.True(result.Success);
        _otpService.Verify(o => o.GenerateVerificationOtp(), Times.Never);
        _backgroundJobClient.Verify(b => b.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Never);
    }
}

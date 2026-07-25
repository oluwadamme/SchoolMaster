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
    private readonly Mock<ICurrentTenant> _currentTenant = new();
    private readonly Mock<IOtpService> _otpService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly IOptions<EmailVerificationOptions> _emailOptions =
        Options.Create(new EmailVerificationOptions { ExpirationInMinutes = 15 });

    private OnboardingService CreateSut() => new(
        _tenantRepo.Object,
        _userRepo.Object,
        _emailOptions,
        _currentTenant.Object,
        _otpService.Object,
        _unitOfWork.Object);

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
        _userRepo.Setup(r => r.ExistsByEmailInTenantAsync(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(false);
        _tenantRepo.Setup(r => r.ExistsBySubdomainAsync(It.IsAny<string>())).ReturnsAsync(false);
        _otpService.Setup(o => o.GenerateVerificationOtp()).Returns("1234");

        var result = await CreateSut().CreateTenantWithAdminAsync(MakeValidRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotEqual(Guid.Empty, result.Data.TenantId);
        Assert.Equal(MakeValidRequest().Subdomain, result.Data.Subdomain);
        Assert.True(result.Data.EmailVerificationRequired);
    }

    [Fact]
    public async Task CreateTenantWithAdminAsync_WithValidRequest_CreatesTenantAndUnverifiedAdmin()
    {
        _userRepo.Setup(r => r.ExistsByEmailInTenantAsync(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(false);
        _tenantRepo.Setup(r => r.ExistsBySubdomainAsync(It.IsAny<string>())).ReturnsAsync(false);
        _otpService.Setup(o => o.GenerateVerificationOtp()).Returns("1234");

        await CreateSut().CreateTenantWithAdminAsync(MakeValidRequest());

        _tenantRepo.Verify(r => r.AddTenantAsync(It.Is<Tenant>(t =>
            t.Status == TenantStatus.Active && t.Plan == TenantPlan.Basic)), Times.Once);

        _userRepo.Verify(r => r.AddUserAsync(It.Is<User>(u =>
            u.Roles.Contains(UserRole.Admin) && !u.IsEmailVerified && u.OtpToken == "1234")), Times.Once);
    }

    [Fact]
    public async Task CreateTenantWithAdminAsync_WithValidRequest_AddsOtpVerificationEventToUser()
    {
        _userRepo.Setup(r => r.ExistsByEmailInTenantAsync(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(false);
        _tenantRepo.Setup(r => r.ExistsBySubdomainAsync(It.IsAny<string>())).ReturnsAsync(false);
        _otpService.Setup(o => o.GenerateVerificationOtp()).Returns("1234");

        await CreateSut().CreateTenantWithAdminAsync(MakeValidRequest());

        _userRepo.Verify(r => r.AddUserAsync(It.Is<User>(u => 
            u.DomainEvents.Any(e => e.GetType().Name == "OtpVerificationEvent"))), Times.Once);
    }

    [Fact]
    public async Task CreateTenantWithAdminAsync_WhenAdminEmailAlreadyExists_ThrowsAlreadyExistException()
    {
        _userRepo.Setup(r => r.ExistsByEmailInTenantAsync(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<AlreadyExistException>(
            () => CreateSut().CreateTenantWithAdminAsync(MakeValidRequest()));

        // Tenant should NOT have been created
        _tenantRepo.Verify(r => r.AddTenantAsync(It.IsAny<Tenant>()), Times.Never);
    }

    [Fact]
    public async Task CreateTenantWithAdminAsync_WhenSubdomainAlreadyExists_ThrowsAlreadyExistException()
    {
        _userRepo.Setup(r => r.ExistsByEmailInTenantAsync(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(false);
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
        _userRepo.Setup(r => r.GetUserByEmailAsync(user.Email)).ReturnsAsync(user);

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
        _userRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
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
        _userRepo.Setup(r => r.GetUserByEmailAsync(user.Email)).ReturnsAsync(user);

        await Assert.ThrowsAsync<InvalidOtpException>(
            () => CreateSut().VerifyUserEmailAsync(new VerifyUserEmailRequest
            {
                Email = user.Email, OtpToken = "wrong"
            }));

        // A genuine wrong guess against a live OTP must be counted AND persisted. The method throws,
        // which stops UnitOfWorkFilter from committing, so the service must save explicitly — otherwise
        // the attempt cap never advances and the OTP can be brute-forced.
        Assert.Equal(1, user.OtpAttemptCount);
        _userRepo.Verify(r => r.UpdateUserAsync(user), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
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
        _userRepo.Setup(r => r.GetUserByEmailAsync(user.Email)).ReturnsAsync(user);

        // VerifyUserEmailAsync bundles all failure modes in one if-statement,
        // so expired OTP produces InvalidOtpException, not OtpExpiredException.
        await Assert.ThrowsAsync<InvalidOtpException>(
            () => CreateSut().VerifyUserEmailAsync(new VerifyUserEmailRequest
            {
                Email = user.Email, OtpToken = "1234"
            }));

        // An expired OTP is not a "genuine wrong guess", so it must not count toward the lockout
        // or trigger a persist.
        Assert.Equal(0, user.OtpAttemptCount);
        _unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    // -------------------------------------------------------------------------
    // ResendVerificationOtpAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ResendVerificationOtpAsync_WithUnverifiedUser_StoresNewOtpAndAddsEvent()
    {
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "A", LastName = "B",
            Email = "a@test.com", PasswordHash = "h", Roles = [UserRole.Admin],
            IsEmailVerified = false, OtpToken = "old-otp",
        };
        _currentTenant.SetupGet(t => t.Id).Returns(tenantId);
        _userRepo.Setup(r => r.GetUserByEmailAsync(user.Email)).ReturnsAsync(user);
        _otpService.Setup(o => o.GenerateVerificationOtp()).Returns("new-otp");

        var result = await CreateSut().ResendVerificationOtpAsync(new ResendOtpRequest { Email = user.Email });

        Assert.True(result.Success);
        Assert.Equal("new-otp", user.OtpToken);
        _userRepo.Verify(r => r.UpdateUserAsync(user), Times.Once);
        Assert.Contains(user.DomainEvents, e => e.GetType().Name == "OtpVerificationEvent");
    }

    [Fact]
    public async Task ResendVerificationOtpAsync_WithEmptyTenantId_ReturnsSilentSuccess()
    {
        // With no resolved tenant the global query filter scopes the lookup to TenantId == Guid.Empty,
        // which matches nobody, so the flow returns the same silent success (no account enumeration)
        // and never issues a new OTP.
        _currentTenant.SetupGet(t => t.Id).Returns(Guid.Empty);
        _userRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        var result = await CreateSut().ResendVerificationOtpAsync(new ResendOtpRequest { Email = "a@b.com" });

        Assert.True(result.Success);
        _userRepo.Verify(r => r.UpdateUserAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task ResendVerificationOtpAsync_WithUnknownEmail_ReturnsSilentSuccessWithoutSendingEmail()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.SetupGet(t => t.Id).Returns(tenantId);
        _userRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
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
        _userRepo.Setup(r => r.GetUserByEmailAsync(user.Email)).ReturnsAsync(user);

        var result = await CreateSut().ResendVerificationOtpAsync(new ResendOtpRequest { Email = user.Email });

        Assert.True(result.Success);
        _otpService.Verify(o => o.GenerateVerificationOtp(), Times.Never);
        _backgroundJobClient.Verify(b => b.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Never);
    }
}

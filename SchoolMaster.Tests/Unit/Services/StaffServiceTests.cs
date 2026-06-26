using Moq;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.Services;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using Xunit;
using Microsoft.Extensions.Options;
using SchoolMaster.Infrastructure.Options;
using Hangfire;
using SchoolMaster.Application.DTOs;

namespace SchoolMaster.Tests.Unit.Services;

public class StaffServiceTests
{
    // These are the "Toy Tools" (Mocks)
    // also at the same time, creating all the dependencies of the StaffService
    // these contain the fake dependencies
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IStaffRepository> _staffRepo = new();
    private readonly Mock<ITenantRepository> _tenantRepo = new();
    // using a mock current tenant becuase the real has to check website request to generate the tenant id, but there is no website request, so we use a mock to return a fake tenant id
    private readonly Mock<ICurrentTenant> _currentTenant = new();
    // using a fake otp generator to predict the otp code that will be generated, so we can test the email verification process
    private readonly Mock<IOtpService> _otpService = new();
    private readonly Mock<IBackgroundJobClient> _jobClient = new();

    // creating new EmailVerificatioOptions

    private readonly IOptions<EmailVerificationOptions> _options = 
        Microsoft.Extensions.Options.Options.Create(new EmailVerificationOptions());

    // Make a new staff Object but fill the dependencies with Mocks.(_userRepo.Object e.t.c) -> these are the actual "fake" dependencies 
    private StaffService CreateSut() => new(
        _userRepo.Object,
        _staffRepo.Object,
        _tenantRepo.Object,
        _currentTenant.Object,
        _otpService.Object,
        _jobClient.Object,
        _options);

    [Fact]
    public async Task GenerateStaffNumber_WhenNoStaffExists_StartsAtOne()
    {
        // 1. Arrange: Setup our "Toy Tools"
        var tenantId = Guid.NewGuid();
        var schoolCode = "GHA";
        var year = DateTime.UtcNow.Year;

        // Tell the "School Tool" to say the school code is "GHA"
        // when you ask for GetByIdAsync in the tenant repo, you tell the mock to return a tenant object with ID and schoolcode you have defined
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId))
            .ReturnsAsync(new Tenant {
                Id = tenantId,
                SchoolCode = schoolCode,
                Name = "Test School",
                Subdomain = "testschool",
                ContactEmail = "contact@testschool.edu",
                Status = Domain.Enums.TenantStatus.Active,
                Plan = TenantPlan.Free
            });

        // Tell the "Staff Tool" to say there are NO staff members yet (null)
        _staffRepo.Setup(r => r.GetLastStaffNumberAsync(tenantId, It.IsAny<string>()))
            .ReturnsAsync((string?)null);
            
        // Tell the "Current School Tool" which school we are in
        _currentTenant.Setup(t => t.Id).Returns(tenantId);

        // Note: For this example, we test the logic inside the service\
        // we bring the created staffService in line 34 to test
        var service = CreateSut();
        
        // creating staff
        var request = new CreateStaffRequest("James", "Teacher", "james@test.com", "Pass123!", "Science", StaffRole.Teacher, EmploymentType.FullTime);
        var result = await service.CreateStaffAsync(request);

        // 3. Assert: Check if the number is GHA/STF/2024/000001
        var expected = $"{schoolCode}/STF/{year}/000001";
        Assert.Equal(expected, result.Data?.StaffNumber);
    }
}
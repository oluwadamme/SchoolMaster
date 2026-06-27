using Microsoft.Extensions.Hosting;
using Moq;
using SchoolMaster.Application.Services;
using Xunit;

namespace SchoolMaster.Tests.Unit.Services;

public class OtpServiceTests
{
    [Fact]
    public void GenerateVerificationOtp_WhenDevelopment_ReturnsSixZeros()
    {
        // Arrange
        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(m => m.EnvironmentName).Returns(Environments.Development);
        var service = new OtpService(mockEnv.Object);

        // Act
        var result = service.GenerateVerificationOtp();

        // Assert
        Assert.Equal("000000", result);
    }

    [Fact]
    public void GenerateVerificationOtp_WhenNotDevelopment_ReturnsFourDigitString()
    {
        // Arrange
        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(m => m.EnvironmentName).Returns(Environments.Production);
        var service = new OtpService(mockEnv.Object);

        // Act
        var result = service.GenerateVerificationOtp();

        // Assert
        Assert.Equal(4, result.Length);
        Assert.True(int.TryParse(result, out _));
    }
}

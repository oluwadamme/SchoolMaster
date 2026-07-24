using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Text.RegularExpressions;
using SchoolMaster.Application.Services;
using Xunit;

namespace SchoolMaster.Tests.Unit.Services;

public class OtpServiceTests
{
    [Fact]
    public void GenerateVerificationOtp_ReturnsSixDigitNumericCode()

    {
        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(m => m.EnvironmentName).Returns(Environments.Development);
        var service = new OtpService(mockEnv.Object);
        var otp = service.GenerateVerificationOtp();

        Assert.Matches(new Regex("^[0-9]{6}$"), otp);
    }

    [Fact]
    public void GenerateVerificationOtp_ProducesVaryingValues()
    {
        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(m => m.EnvironmentName).Returns(Environments.Production);
        var service = new OtpService(mockEnv.Object);

        // Not a strict randomness test, just a guard that it is not a constant.
        var values = Enumerable.Range(0, 20).Select(_ => service.GenerateVerificationOtp()).ToHashSet();

        Assert.True(values.Count > 1);
    }

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
    public void GenerateVerificationOtp_WhenNotDevelopment_ReturnsSixDigitString()
    {
        // Arrange
        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(m => m.EnvironmentName).Returns(Environments.Production);
        var service = new OtpService(mockEnv.Object);

        // Act
        var result = service.GenerateVerificationOtp();

        // Assert
        Assert.Equal(6, result.Length);
        Assert.True(int.TryParse(result, out _));
    }

    [Fact]
    public void GenerateVerificationOtp_WhenDevelopment_ReturnsSixDigitNumericCode()
    {
        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(m => m.EnvironmentName).Returns(Environments.Development);
        var otp = new OtpService(mockEnv.Object).GenerateVerificationOtp();

        Assert.Matches(new Regex("^[0-9]{6}$"), otp);
    }

    [Fact]
    public void GenerateVerificationOtp_WhenNotDevelopment_ProducesVaryingValues()
    {
        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(m => m.EnvironmentName).Returns(Environments.Production);
        var service = new OtpService(mockEnv.Object);

        // Not a strict randomness test, just a guard that it is not a constant.
        var values = Enumerable.Range(0, 20).Select(_ => service.GenerateVerificationOtp()).ToHashSet();

        Assert.True(values.Count > 1);
    }
}

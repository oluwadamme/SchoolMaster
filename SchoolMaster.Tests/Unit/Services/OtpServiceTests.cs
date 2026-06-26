using System.Text.RegularExpressions;
using SchoolMaster.Application.Services;
using Xunit;

namespace SchoolMaster.Tests.Unit.Services;

public class OtpServiceTests
{
    [Fact]
    public void GenerateVerificationOtp_ReturnsSixDigitNumericCode()
    {
        var otp = new OtpService().GenerateVerificationOtp();

        Assert.Matches(new Regex("^[0-9]{6}$"), otp);
    }

    [Fact]
    public void GenerateVerificationOtp_ProducesVaryingValues()
    {
        var service = new OtpService();

        // Not a strict randomness test, just a guard that it is not a constant.
        var values = Enumerable.Range(0, 20).Select(_ => service.GenerateVerificationOtp()).ToHashSet();

        Assert.True(values.Count > 1);
    }
}

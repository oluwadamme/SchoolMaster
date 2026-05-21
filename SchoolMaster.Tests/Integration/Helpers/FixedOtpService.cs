using SchoolMaster.Application.Services.Interfaces;

namespace SchoolMaster.Tests.Integration.Helpers;

/// <summary>
/// Always returns the same OTP so integration tests can predict the value
/// that gets stored in the database without relying on the real random generator.
/// </summary>
public class FixedOtpService(string otp) : IOtpService
{
    public string GenerateVerificationOtp() => otp;
}

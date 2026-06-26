using System.Security.Cryptography;
using SchoolMaster.Application.Services.Interfaces;

namespace SchoolMaster.Application.Services;

public class OtpService : IOtpService
{
    public string GenerateVerificationOtp()
    {
        // 6 digits (000000-999999). A 4-digit code only has 10k combinations, which is brute-forceable
        // within the OTP lifetime; 6 digits raises that to 1,000,000 and pairs with the per-account
        // attempt lockout enforced when the OTP is verified.
        return RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
    }
}

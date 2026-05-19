using System.Security.Cryptography;
using SchoolMaster.Application.Services.Interfaces;

namespace SchoolMaster.Application.Services;

public class OtpService : IOtpService
{
    public string GenerateVerificationOtp()
    {
        return RandomNumberGenerator.GetInt32(10000).ToString("D4");
    }
}

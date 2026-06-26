using System.Security.Cryptography;
using SchoolMaster.Application.Services.Interfaces;

namespace SchoolMaster.Application.Services;

public class OtpService : IOtpService
{
    private readonly IHostEnvironment _env;

    public OtpService(IHostEnvironment env)
    {
        _env = env;
    }

    public string GenerateVerificationOtp()
    {
        if (_env.IsDevelopment())
        {
            return "000000";
        }

        return RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
    }
}

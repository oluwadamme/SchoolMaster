using SchoolMaster.Application.DTOs;

namespace SchoolMaster.Application.Services.Interfaces;

public interface IOnboardingService
{
    Task<BaseResponse<Guid>> CreateTenantWithAdminAsync(OnboardTenantRequest request);
    Task<BaseResponse<bool>> VerifyUserEmailAsync(VerifyUserEmailRequest request);
    Task<BaseResponse<bool>> ResendVerificationOtpAsync(ResendOtpRequest request);

    Task<BaseResponse<bool>> ForgotPasswordAsync(ForgetPasswordRequest request);
    Task<BaseResponse<bool>> ResetPasswordAsync(ResetPasswordRequest request);
}
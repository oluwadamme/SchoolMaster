namespace SchoolMaster.Application.Services.Interfaces;

public interface INotificationJob
{
    Task SendAsync(Guid tenantId, Guid studentId, DateOnly date);
    Task SendOtpVerificationAsync(Guid tenantId, string userEmail, string userName, string otpCode);
}

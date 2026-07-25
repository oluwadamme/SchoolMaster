namespace SchoolMaster.Application.Services.Interfaces;

// Abstracts Hangfire behind an Application interface so the MediatR handler
// (Infrastructure) does not depend on Hangfire directly, and tests can register a no-op.
public interface IJobScheduler
{
    void ScheduleAbsenceNotification(Guid tenantId, Guid studentId, DateOnly date);
    void ScheduleOtpVerificationNotification(Guid tenantId, string userEmail, string userName, string otpCode);
}

using Hangfire;
using SchoolMaster.Application.Services.Interfaces;

namespace SchoolMaster.Infrastructure.Jobs;

public class HangfireJobScheduler(IBackgroundJobClient client) : IJobScheduler
{
    public void ScheduleAbsenceNotification(Guid tenantId, Guid studentId, DateOnly date) =>
        client.Enqueue<INotificationJob>(job => job.SendAsync(tenantId, studentId, date));

    public void ScheduleOtpVerificationNotification(Guid tenantId, string userEmail, string userName, string otpCode) =>
        client.Enqueue<INotificationJob>(job => job.SendOtpVerificationAsync(tenantId, userEmail, userName, otpCode));
}

using Hangfire;
using SchoolMaster.Application.Services.Interfaces;

namespace SchoolMaster.Infrastructure.Jobs;

public class HangfireAttendanceJobScheduler(IBackgroundJobClient client) : IAttendanceJobScheduler
{
    public void ScheduleAbsenceNotification(Guid tenantId, Guid studentId, DateOnly date) =>
        client.Enqueue<IAbsenceNotificationJob>(job => job.SendAsync(tenantId, studentId, date));
}

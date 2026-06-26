using SchoolMaster.Application.Services.Interfaces;

namespace SchoolMaster.Infrastructure.Jobs;

// Registered in the Testing environment — integration tests don't need a real Hangfire server.
public class NoOpAttendanceJobScheduler : IAttendanceJobScheduler
{
    public void ScheduleAbsenceNotification(Guid tenantId, Guid studentId, DateOnly date) { }
}

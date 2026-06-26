using MediatR;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.Events;

namespace SchoolMaster.Infrastructure.EventHandlers;

public class StudentMarkedAbsentEventHandler(IAttendanceJobScheduler scheduler)
    : INotificationHandler<StudentMarkedAbsentEvent>
{
    public Task Handle(StudentMarkedAbsentEvent notification, CancellationToken cancellationToken)
    {
        scheduler.ScheduleAbsenceNotification(
            notification.TenantId, notification.StudentId, notification.Date);
        return Task.CompletedTask;
    }
}

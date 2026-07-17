using MediatR;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.Events;

namespace SchoolMaster.Infrastructure.EventHandlers;

public class StudentMarkedAbsentEventHandler(IJobScheduler scheduler)
    : INotificationHandler<StudentMarkedAbsentEvent>
    // : INotificationHandler<StudentMarkedAbsentEvent> Tells MediatR that this class can handle events of type
    // StudentMarkedAbsentEvent. MediatR will call the Handle method whenever such an event is published.
{
    public Task Handle(StudentMarkedAbsentEvent notification, CancellationToken cancellationToken)
    {
        // the ScheduleAbsenceNotification method in the class that uses the interface IAttendanceJobScheduler is called to
        // send notifications to parents/guardians about their child's absence. 
        scheduler.ScheduleAbsenceNotification(
            notification.TenantId, notification.StudentId, notification.Date);
        return Task.CompletedTask;
    }
}

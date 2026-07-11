using MediatR;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.Events;

namespace SchoolMaster.Infrastructure.EventHandlers;

public class OtpVerificationEventHandler(IJobScheduler scheduler)
    : INotificationHandler<OtpVerificationEvent>
// : INotificationHandler<OtpVerificationEvent> Tells MediatR that this class can handle events of type
// OtpVerificationEvent. MediatR will call the Handle method whenever such an event is published.
{
    public Task Handle(OtpVerificationEvent notification, CancellationToken cancellationToken)
    {
        // the ScheduleOtpVerificationNotification method in the class that uses the interface IOtpService is called to
        // send OTP verification notifications to users.
        scheduler.ScheduleOtpVerificationNotification(
            notification.TenantId, notification.UserEmail, notification.UserName, notification.OtpCode);
        return Task.CompletedTask;
    }
}

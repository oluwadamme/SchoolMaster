namespace SchoolMaster.Application.Services.Interfaces;

public interface IAbsenceNotificationJob
{
    Task SendAsync(Guid tenantId, Guid studentId, DateOnly date);
}

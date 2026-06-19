using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.Services.Interfaces;

namespace SchoolMaster.Infrastructure.Jobs;

public class AbsenceNotificationJob(
    IStudentRepository studentRepo,
    ITenantRepository tenantRepo,
    IEmailService emailService) : IAbsenceNotificationJob
{
    public async Task SendAsync(Guid tenantId, Guid studentId, DateOnly date)
    {
        // WARNING: there is no HttpContext here, so ICurrentTenant.Id resolves to Guid.Empty and
        // the SchoolMasterContext global query filter would silently return zero rows. Every query
        // in this job MUST either target a filter-free entity (Tenant) or bypass the filter with an
        // explicit tenantId (GetStudentByIdIgnoringFiltersAsync). Do not add a plain filtered query here.

        // Sequential, not parallel: both repos share this job's scoped DbContext, and EF Core
        // forbids concurrent operations on a single context instance.
        var student = await studentRepo.GetStudentByIdIgnoringFiltersAsync(studentId, tenantId);
        if (student is null) return; // Student withdrawn between marking and job running — skip

        var tenant = await tenantRepo.GetByIdAsync(tenantId);
        var schoolName = tenant?.Name ?? "SchoolMaster";

        var subject = $"Absence Notification — {date:MMMM d, yyyy}";
        var body = $"""
            Dear {student.GuardianName},

            This is to inform you that {student.FirstName} {student.LastName} was marked absent on {date:MMMM d, yyyy}.

            If you have already notified the school, please disregard this message.

            Regards,
            {schoolName}
            """;

        await emailService.SendEmailAsync(student.GuardianEmail, student.GuardianName, subject, body);
    }
}

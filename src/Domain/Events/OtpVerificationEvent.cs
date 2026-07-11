using SchoolMaster.Domain.Common;
namespace SchoolMaster.Domain.Events;

public record OtpVerificationEvent(
    Guid TenantId,
    string UserEmail,
    string UserName,
    string OtpCode
) : IDomainEvent;
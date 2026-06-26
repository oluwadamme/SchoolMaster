using SchoolMaster.Domain.Common;

namespace SchoolMaster.Domain.Events;

public record StudentMarkedAbsentEvent(
    Guid TenantId,
    Guid StudentId,
    DateOnly Date
) : IDomainEvent;

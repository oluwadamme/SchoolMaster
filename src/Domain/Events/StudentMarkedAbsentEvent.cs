using SchoolMaster.Domain.Common;

namespace SchoolMaster.Domain.Events;
// class that implements IDomainEvent is an event that holds its own notification(message)
// The event will be published in UnitOfWork using MediatR
public record StudentMarkedAbsentEvent(
    Guid TenantId,
    Guid StudentId,
    DateOnly Date
) : IDomainEvent;

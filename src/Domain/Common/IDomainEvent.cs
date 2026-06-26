using MediatR;
namespace SchoolMaster.Domain.Common;

// Marker interface. Extends MediatR's INotification so handlers can subscribe.
public interface IDomainEvent : INotification { }

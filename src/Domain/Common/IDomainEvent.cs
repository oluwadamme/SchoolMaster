using MediatR;
namespace SchoolMaster.Domain.Common;

// Marker interface. Extends MediatR's INotification so handlers can subscribe.
// Any class that implements this interface is a notification(message) that mediaTR can publish and  handle
public interface IDomainEvent : INotification { }

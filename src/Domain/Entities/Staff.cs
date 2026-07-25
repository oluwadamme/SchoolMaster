using SchoolMaster.Domain.Common;
namespace SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using System.Diagnostics.CodeAnalysis;

public class Staff : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public required Guid Id { get; set; }
    public required Guid UserId { get; set; }
    public required Guid TenantId { get; set; }
    public required string StaffNumber { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Department { get; set; }
    public required StaffRole StaffRole { get; set; }
    public required EmploymentType EmploymentType { get; set; }
    public string? Qualifications { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTime EmployedAt { get; set; }
    public required StaffStatus Status { get; set; }

    public User User { get; set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public static Staff Create(Guid tenantId, Guid userId, string staffNumber, string firstName, string lastName,
        string department, StaffRole staffRole, EmploymentType employmentType, string? qualifications,
        string? photoUrl, DateTime employedAt, StaffStatus status)
    {
        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            StaffNumber = staffNumber,
            FirstName = firstName,
            LastName = lastName,
            Department = department,
            StaffRole = staffRole,
            EmploymentType = employmentType,
            Qualifications = qualifications,
            PhotoUrl = photoUrl,
            EmployedAt = employedAt,
            Status = status
        };

        return staff;
    }

}
using SchoolMaster.Domain.Common;
using SchoolMaster.Domain.Enums;
using SchoolMaster.Domain.Events;

namespace SchoolMaster.Domain.Entities;

public class DailyAttendance : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid ClassId { get; private set; }
    public Guid TermId { get; private set; }
    public DateOnly Date { get; private set; }
    public AttendanceStatus Status { get; private set; }
    public Guid MarkedByTeacherId { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public static DailyAttendance Create(Guid tenantId, Guid studentId, Guid classId,
        Guid termId, DateOnly date, AttendanceStatus status, Guid markedByTeacherId, string? notes)
    {
        var record = new DailyAttendance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StudentId = studentId,
            ClassId = classId,
            TermId = termId,
            Date = date,
            Status = status,
            MarkedByTeacherId = markedByTeacherId,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        // When a student is marked absent, the system must do two things:

// Notify the rest of the program that “this student was absent on this date”.
// Schedule a background job that will later send a notification (for example, an email or a push message) to the student’s parents or school staff.

        if (status == AttendanceStatus.Absent)
        // creates an event that says the student was absent on this day for any students that was absent
        // in the collection of student records passed in the request and adds all events to _domainEvents
        // event being new StudentMarkedAbsentEvent() 
            record._domainEvents.Add(new StudentMarkedAbsentEvent(tenantId, studentId, date));

        return record;
    }

    public void UpdateStatus(AttendanceStatus status, Guid markedByTeacherId, string? notes)
    {
        var wasAbsent = Status == AttendanceStatus.Absent;
        Status = status;
        MarkedByTeacherId = markedByTeacherId;
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;

        // Only fire event if this is a NEW absence — don't re-notify if already absent
        if (status == AttendanceStatus.Absent && !wasAbsent)
            _domainEvents.Add(new StudentMarkedAbsentEvent(TenantId, StudentId, Date));
    }
}

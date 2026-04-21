namespace SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
// Domain layer — school identity
public class Student
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }          // → User (for login)
    public Guid TenantId { get; private set; }
    public Guid ClassId { get; private set; }
    public string StudentNumber { get; private set; } // STU-2024-00142
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public DateOnly DateOfBirth { get; private set; }
    public Gender Gender { get; private set; }
    public string GuardianName { get; private set; }
    public string GuardianPhone { get; private set; }
    public string GuardianEmail { get; private set; }
    public string? MedicalNotes { get; private set; }
    public string? PhotoUrl { get; private set; }
    public StudentStatus Status { get; private set; } // Active, Transferred, Withdrawn
    public DateTime EnrolledAt { get; private set; }

    public User User { get; private set; }
    // public Class Class { get; private set; }
    // public ICollection<AttendanceRecord> AttendanceRecords { get; private set; }
    // public ICollection<AcademicResult> Results { get; private set; }
}

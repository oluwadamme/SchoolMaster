namespace SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// Domain layer — school identity
public class Student
{
    public required Guid Id { get; set; }
    public required Guid UserId { get; set; }
    public required Guid TenantId { get; set; }
    public Guid ClassId { get; set; }
    public required string StudentNumber { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required DateOnly DateOfBirth { get; set; }
    public required Gender Gender { get; set; }
    public required string GuardianName { get; set; }
    public required string GuardianPhone { get; set; }
    public required string GuardianEmail { get; set; }
    public string? MedicalNotes { get; set; }
    public string? PhotoUrl { get; set; }
    public required StudentStatus Status { get; set; }
    public required DateTime EnrolledAt { get; set; }

    public User User { get; set; }

  
}

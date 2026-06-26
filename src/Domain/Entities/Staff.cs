namespace SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using System.Diagnostics.CodeAnalysis;

public class Staff
{
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



}
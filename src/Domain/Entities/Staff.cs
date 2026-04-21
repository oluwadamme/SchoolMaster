namespace SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
public class Staff
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string StaffNumber { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string Department { get; private set; }
    public StaffRole StaffRole { get; private set; } // 
    public EmploymentType EmploymentType { get; private set; }
    public string? Qualifications { get; private set; }
    public string? PhotoUrl { get; private set; }
    public DateTime EmployedAt { get; private set; }
    public StaffStatus Status { get; private set; } 

    public User User { get; private set; }
    // public ICollection<SubjectAssignment> SubjectAssignments { get; private set; }
    // public ICollection<LeaveRequest> LeaveRequests { get; private set; }
}
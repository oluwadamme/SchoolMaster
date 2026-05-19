namespace SchoolMaster.Application.DTOs;

using SchoolMaster.Domain.Enums;

public record CreateStaffRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string StaffNumber,
    string Department,
    StaffRole StaffRole,
    EmploymentType EmploymentType
);
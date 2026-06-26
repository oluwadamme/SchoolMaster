namespace SchoolMaster.Application.DTOs;
using SchoolMaster.Domain.Enums;

public record StaffResponse(
    Guid Id,
    Guid UserId,
    Guid TenantId,
    string StaffNumber,
    string FirstName,
    string LastName,
    string Department,
    StaffRole StaffRole,
    EmploymentType EmploymentType
);
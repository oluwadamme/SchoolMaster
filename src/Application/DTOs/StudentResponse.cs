using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Application.DTOs;

public record StudentResponse(
    Guid Id,
    Guid TenantId,
    string FirstName,
    string LastName,
    string StudentNumber,
    DateOnly DateOfBirth,
    Gender Gender,
    string GuardianName,
    string GuardianPhone,
    string GuardianEmail,
    string? PhotoUrl

);
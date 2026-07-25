using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Application.DTOs;

/// <summary>
/// This container holds the Token (ID Badge) and the user's details.
/// </summary>
public record AuthResponse(string Token, string RefreshToken, Guid Id, Guid TenantId, string Email, string FirstName, string LastName, List<UserRole> Roles, bool IsEmailVerified);
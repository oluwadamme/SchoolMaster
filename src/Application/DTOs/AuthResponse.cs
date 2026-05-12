namespace SchoolMaster.Application.DTOs;

/// <summary>
/// This container holds the Token (ID Badge) and the user's details.
/// </summary>
public record AuthResponse(string Token, string FirstName, string LastName, string Role);
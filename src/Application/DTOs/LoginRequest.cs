namespace SchoolMaster.Application.DTOs;

/// <summary>
/// This container holds the email and password sent by the user.
/// </summary>
public record LoginRequest(string Email, string Password);
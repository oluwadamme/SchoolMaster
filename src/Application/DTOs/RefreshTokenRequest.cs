namespace SchoolMaster.Application.DTOs;

/// <summary>
/// This box is used when the app asks for a new Access Token.
/// </summary>
public record RefreshTokenRequest(string AccessToken, string RefreshToken);
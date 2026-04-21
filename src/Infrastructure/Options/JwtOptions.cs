namespace SchoolMaster.Infrastructure.Options;

public class JwtOptions
{
    public required string Key { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public int ExpirationInMinutes { get; set; }
    public int RefreshTokenExpirationInDays { get; set; }
}
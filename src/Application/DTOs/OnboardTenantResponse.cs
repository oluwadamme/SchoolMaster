namespace SchoolMaster.Application.DTOs;

public record OnboardTenantResponse(
    Guid TenantId,
    string SchoolName,
    string Subdomain,
    string AdminEmail,
    bool EmailVerificationRequired
);

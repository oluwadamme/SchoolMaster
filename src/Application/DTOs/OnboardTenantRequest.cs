using System;
namespace SchoolMaster.Application.DTOs;

public class OnboardTenantRequest
{
    // Tenant info
    public string SchoolName { get; set; }
    public string Subdomain { get; set; }
    public string ContactEmail { get; set; }

    // Admin info
    public string AdminEmail { get; set; }
    public string AdminPassword { get; set; }
}

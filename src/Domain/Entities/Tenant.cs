namespace SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
public class Tenant
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Subdomain { get; set; }
    public required string SchoolCode { get; set; }
    public required string ContactEmail { get; set; }
    public required TenantStatus Status { get; set; }
    public required TenantPlan Plan { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
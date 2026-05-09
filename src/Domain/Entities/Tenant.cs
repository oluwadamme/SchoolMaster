namespace SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; }        // "Greenfield Academy"
    public string Subdomain { get; set; }   // "greenfield" → greenfield.yourapp.com
    public string ContactEmail { get; set; }
    public TenantStatus Status { get; set; }
    public TenantPlan Plan { get; set; }    // Free, Basic, Pro
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
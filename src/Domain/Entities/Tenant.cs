namespace SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
public class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }        // "Greenfield Academy"
    public string Subdomain { get; private set; }   // "greenfield" → greenfield.yourapp.com
    public string ContactEmail { get; private set; }
    public TenantStatus Status { get; private set; } 
    public TenantPlan Plan { get; private set; }    // Free, Basic, Pro
    public DateTime CreatedAt { get; private set; }

    public Tenant(Guid id, string name, string subdomain, string contactEmail, TenantStatus status, TenantPlan plan, DateTime createdAt)
    {
        Id = id;
        Name = name;
        Subdomain = subdomain;
        ContactEmail = contactEmail;
        Status = status;
        Plan = plan;
        CreatedAt = createdAt;
    }
}
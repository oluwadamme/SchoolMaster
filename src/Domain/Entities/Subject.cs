// src/Domain/Entities/Subject.cs
namespace SchoolMaster.Domain.Entities;

public class Subject
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }         // "Mathematics"
    public string? Code { get; private set; }        // "MATH" — optional shortcode
    public DateTime CreatedAt { get; private set; }

    public static Subject Create(Guid tenantId, string name, string? code = null)
    {
        return new Subject
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Code = code,
            CreatedAt = DateTime.UtcNow
        };
    }
}

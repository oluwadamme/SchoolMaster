// src/Domain/Entities/Class.cs
namespace SchoolMaster.Domain.Entities;

public class Class
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }         // "JSS 1A", "Primary 3B"
    public Guid? FormTeacherId { get; private set; } // optional — Staff.Id
    public DateTime CreatedAt { get; private set; }

    public static Class Create(Guid tenantId, string name, Guid? formTeacherId = null)
    {
        return new Class
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            FormTeacherId = formTeacherId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AssignFormTeacher(Guid teacherId) => FormTeacherId = teacherId;
}

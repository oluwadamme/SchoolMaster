// src/Domain/Entities/AcademicYear.cs
namespace SchoolMaster.Domain.Entities;


public class AcademicYear
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }         // "2025/2026"
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsCurrent { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public ICollection<Term> Terms { get; private set; } = new List<Term>();

    public static AcademicYear Create(Guid tenantId, string name, DateOnly start, DateOnly end, bool isCurrent)
    {
        return new AcademicYear
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            StartDate = start,
            EndDate = end,
            IsCurrent = isCurrent,
            CreatedAt = DateTime.UtcNow
        };
    }

    

    public void SetAsCurrent() => IsCurrent = true;
    public void UnsetCurrent() => IsCurrent = false;

    public void Update(string name, DateOnly startDate, DateOnly endDate)
    {
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
    }
}

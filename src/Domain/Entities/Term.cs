// src/Domain/Entities/Term.cs
namespace SchoolMaster.Domain.Entities;

public class Term
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid AcademicYearId { get; private set; }
    public string Name { get; private set; }         // "First Term"
    public int TermNumber { get; private set; }      // 1, 2, 3 — for ordering
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsCurrent { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public AcademicYear AcademicYear { get; private set; }

    public static Term Create(Guid tenantId, Guid academicYearId, string name, int termNumber,
        DateOnly start, DateOnly end, bool isCurrent)
    {
        return new Term
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AcademicYearId = academicYearId,
            Name = name,
            TermNumber = termNumber,
            StartDate = start,
            EndDate = end,
            IsCurrent = isCurrent,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetAsCurrent() => IsCurrent = true;
    public void UnsetCurrent() => IsCurrent = false;
}

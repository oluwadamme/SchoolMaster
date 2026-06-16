
// src/Domain/Entities/Period.cs
namespace SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;

public class Period
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ClassId { get; private set; }
    public Guid? SubjectId { get; private set; }     // null for DailyRegister periods
    public Guid? TeacherId { get; private set; }     // Staff.Id — null for DailyRegister
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public PeriodType Type { get; private set; }
    public string Name { get; private set; }         // "Period 1 — Mathematics" or "Morning Register"
    public DateTime CreatedAt { get; private set; }

    public Class Class { get; private set; }
    public Subject? Subject { get; private set; }

    public static Period Create(Guid tenantId, Guid classId, Guid? subjectId, Guid? teacherId,
        DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end, PeriodType type, string name)
    {
        return new Period
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClassId = classId,
            SubjectId = subjectId,
            TeacherId = teacherId,
            DayOfWeek = dayOfWeek,
            StartTime = start,
            EndTime = end,
            Type = type,
            Name = name,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(Guid? subjectId, Guid? teacherId, DayOfWeek dayOfWeek,
        TimeOnly startTime, TimeOnly endTime, PeriodType type, string name)
    {
        SubjectId = subjectId;
        TeacherId = teacherId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        Type = type;
        Name = name;
    }
}

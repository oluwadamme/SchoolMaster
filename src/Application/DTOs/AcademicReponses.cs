namespace SchoolMaster.Application.DTOs;
using SchoolMaster.Domain.Enums;
public record AcademicYearResponse(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsCurrent
);


public record TermResponse(
    Guid Id,
    Guid AcademicYearId,
    string Name,
    int TermNumber,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsCurrent
);


public record ClassResponse(
    Guid Id,
    string Name,
    Guid? FormTeacherId
);


public record SubjectResponse(
    Guid Id,
    string Name,
    string? Code
);


public record PeriodResponse(
    Guid Id,
    Guid ClassId,
    Guid? SubjectId,
    Guid? TeacherId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    PeriodType Type,
    string Name
);


public record TimetableResponse(
    Guid ClassId,
    string ClassName,
    List<PeriodResponse> Periods
);

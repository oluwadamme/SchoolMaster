using SchoolMaster.Domain.Enums;
using FluentValidation;
namespace SchoolMaster.Application.DTOs;
public record CreateAcademicYearRequest(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool SetAsCurrent
);


public record CreateTermRequest(
    Guid AcademicYearId,
    string Name,
    int TermNumber,
    DateOnly StartDate,
    DateOnly EndDate,
    bool SetAsCurrent
);


public record CreateClassRequest(
    string Name,
    Guid? FormTeacherId
);


public record CreateSubjectRequest(
    string Name,
    string? Code
);


public record CreatePeriodRequest(
    Guid? SubjectId,
    Guid? TeacherId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    PeriodType Type,
    string? Name
);

// src/Application/Validators/CreateAcademicYearRequestValidator.cs
public class CreateAcademicYearRequestValidator : AbstractValidator<CreateAcademicYearRequest>
{
    public CreateAcademicYearRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(20);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate)
            .NotEmpty()
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date.");
    }
}

public class CreateTermRequestValidator : AbstractValidator<CreateTermRequest>
{
    public CreateTermRequestValidator()
    {
        RuleFor(x => x.AcademicYearId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TermNumber).InclusiveBetween(1, 3);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate)
            .NotEmpty()
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date.");
    }
}

// src/Application/Validators/CreateClassRequestValidator.cs
public class CreateClassRequestValidator : AbstractValidator<CreateClassRequest>
{
    public CreateClassRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
    }
}

public class CreateSubjectRequestValidator : AbstractValidator<CreateSubjectRequest>
{
    public CreateSubjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code).MaximumLength(20).When(x => x.Code is not null);
    }
}

public class CreatePeriodRequestValidator : AbstractValidator<CreatePeriodRequest>
{
    public CreatePeriodRequestValidator()
    {
        RuleFor(x => x.EndTime)
            .GreaterThan(x => x.StartTime)
            .WithMessage("End time must be after start time.");

        When(x => x.Type == PeriodType.Timetabled, () =>
        {
            RuleFor(x => x.SubjectId)
                .NotNull()
                .WithMessage("SubjectId is required for timetabled periods.");
            RuleFor(x => x.TeacherId)
                .NotNull()
                .WithMessage("TeacherId is required for timetabled periods.");
        });

        // NonAcademic periods have no subject or default name — the user must supply one
        When(x => x.Type == PeriodType.NonAcademic, () =>
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Name is required for non-academic periods (e.g. Break, Lunch, Prep).")
                .MaximumLength(100);
        });

        // For Timetabled and DailyRegister, Name is optional but bounded if provided
        When(x => x.Type != PeriodType.NonAcademic && x.Name is not null, () =>
        {
            RuleFor(x => x.Name).MaximumLength(100);
        });
    }
}

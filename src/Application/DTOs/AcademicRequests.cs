using SchoolMaster.Domain.Enums;
using FluentValidation;
namespace SchoolMaster.Application.DTOs;

// ── Update requests ──────────────────────────────────────────────────────────

public record UpdateAcademicYearRequest(
    string? Name,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool? SetAsCurrent
);

public record UpdateTermRequest(
    string? Name,
    int? TermNumber,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool? SetAsCurrent
);

public record UpdateClassRequest(
    string? Name,
    Optional<Guid?> FormTeacherId
);

public record UpdateSubjectRequest(
    string? Name,
    Optional<string?> Code
);

public record UpdatePeriodRequest(
    Guid? SubjectId,
    Optional<Guid?> TeacherId,
    DayOfWeek? DayOfWeek,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    PeriodType? Type,
    string? Name
);

// ── Update validators ─────────────────────────────────────────────────────────

public class UpdateAcademicYearRequestValidator : AbstractValidator<UpdateAcademicYearRequest>
{
    public UpdateAcademicYearRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(20).When(x => x.Name is not null);
        When(x => x.StartDate.HasValue && x.EndDate.HasValue, () =>
        {
            RuleFor(x => x.EndDate)
                .GreaterThan(x => x.StartDate)
                .WithMessage("End date must be after start date.");
        });
    }
}

public class UpdateTermRequestValidator : AbstractValidator<UpdateTermRequest>
{
    public UpdateTermRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(50).When(x => x.Name is not null);
        RuleFor(x => x.TermNumber).InclusiveBetween(1, 3).When(x => x.TermNumber.HasValue);
        When(x => x.StartDate.HasValue && x.EndDate.HasValue, () =>
        {
            RuleFor(x => x.EndDate)
                .GreaterThan(x => x.StartDate)
                .WithMessage("End date must be after start date.");
        });
    }
}

public class UpdateClassRequestValidator : AbstractValidator<UpdateClassRequest>
{
    public UpdateClassRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(50).When(x => x.Name is not null);
    }
}

public class UpdateSubjectRequestValidator : AbstractValidator<UpdateSubjectRequest>
{
    public UpdateSubjectRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(100).When(x => x.Name is not null);
        RuleFor(x => x.Code.Value).MaximumLength(20)
            .When(x => x.Code.HasValue && x.Code.Value is not null);
    }
}

public class UpdatePeriodRequestValidator : AbstractValidator<UpdatePeriodRequest>
{
    public UpdatePeriodRequestValidator()
    {
        When(x => x.StartTime.HasValue && x.EndTime.HasValue, () =>
        {
            RuleFor(x => x.EndTime)
                .GreaterThan(x => x.StartTime)
                .WithMessage("End time must be after start time.");
        });
    }
}

// ── Create requests ───────────────────────────────────────────────────────────
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

using FluentValidation;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Application.DTOs;

// ── Requests ─────────────────────────────────────────────────────────────────

public record AttendanceEntryRequest(
    Guid StudentId,
    AttendanceStatus Status,
    string? Notes
);

public record MarkAttendanceRequest(
    Guid ClassId,
    DateOnly Date,
    List<AttendanceEntryRequest> Records
);

// ── Responses ────────────────────────────────────────────────────────────────

public record MarkAttendanceResponse(
    int TotalMarked,
    int Present,
    int Absent,
    int Late,
    int Excused,
    int NotificationsQueued
);

public record AttendanceRecordResponse(
    Guid StudentId,
    Guid TermId,
    DateOnly Date,
    AttendanceStatus Status,
    string? Notes
);

public record StudentAttendanceSummaryResponse(
    Guid StudentId,
    int TotalDays,
    int PresentDays,
    int AbsentDays,
    int LateDays,
    int ExcusedDays,
    double AttendancePercentage,
    List<AttendanceRecordResponse> Records
);

public record ClassAttendanceResponse(
    Guid ClassId,
    DateOnly Date,
    List<AttendanceRecordResponse> Records
);

// ── Validators ───────────────────────────────────────────────────────────────

public class MarkAttendanceRequestValidator : AbstractValidator<MarkAttendanceRequest>
{
    public MarkAttendanceRequestValidator()
    {
        RuleFor(x => x.ClassId).NotEmpty();
        RuleFor(x => x.Date)
            .NotEmpty()
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Cannot mark attendance for a future date.");
        RuleFor(x => x.Records)
            .NotEmpty().WithMessage("At least one attendance record is required.")
            .Must(r => r.Select(e => e.StudentId).Distinct().Count() == r.Count)
            .WithMessage("Duplicate student IDs in the same request are not allowed.");
        RuleForEach(x => x.Records).ChildRules(entry =>
        {
            entry.RuleFor(e => e.StudentId).NotEmpty();
            entry.RuleFor(e => e.Notes).MaximumLength(500).When(e => e.Notes is not null);
        });
    }
}

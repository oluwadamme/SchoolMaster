using System;
using SchoolMaster.Domain.Enums;
using FluentValidation;

namespace SchoolMaster.Application.DTOs;

public record UpdateStudentRequest(
    Guid StudentId,
    Optional<string> FirstName = default,
    Optional<string> LastName = default,
    Optional<string> Email = default,
    Optional<DateOnly> DateOfBirth = default,
    Optional<Gender> Gender = default,
    Optional<string> GuardianName = default,
    Optional<string> GuardianPhone = default,
    Optional<string> GuardianEmail = default,
    Optional<string> MedicalNotes = default,
    Optional<string> PhotoUrl = default
);

public class UpdateStudentRequestValidator : AbstractValidator<UpdateStudentRequest>
{
    public UpdateStudentRequestValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty().WithMessage("StudentId is required.");
        When(x => x.FirstName != null, () => {
            RuleFor(x => x.FirstName!).MinimumLength(2).WithMessage("First name must be at least 2 characters.");
        });
        When(x => x.LastName != null, () => {
            RuleFor(x => x.LastName!).MinimumLength(2).WithMessage("Last name must be at least 2 characters.");
        });
        When(x => x.Email != null, () => {
            RuleFor(x => x.Email!).EmailAddress().WithMessage("Invalid email format.");
        });
        When(x => x.DateOfBirth != null, () => {
            RuleFor(x => x.DateOfBirth!.Value)
                .LessThan(DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("Date of birth cannot be in the future.");
        });
        When(x => x.GuardianName != null, () => {
            RuleFor(x => x.GuardianName!).MinimumLength(2).WithMessage("Guardian name must be at least 2 characters.");
        });
        When(x => x.GuardianPhone != null, () => {
            RuleFor(x => x.GuardianPhone!).NotEmpty().WithMessage("Guardian phone is required.");
        });
        When(x => x.GuardianEmail != null, () => {
            RuleFor(x => x.GuardianEmail!).EmailAddress().WithMessage("Invalid guardian email format.");
        });
    }
}

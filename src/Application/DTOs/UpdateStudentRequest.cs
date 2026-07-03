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
        When(x => x.FirstName.HasValue && x.FirstName.Value != null, () => {
            RuleFor(x => x.FirstName.Value!).MinimumLength(2).WithMessage("First name must be at least 2 characters.");
        });
        When(x => x.LastName.HasValue && x.LastName.Value != null, () => {
            RuleFor(x => x.LastName.Value!).MinimumLength(2).WithMessage("Last name must be at least 2 characters.");
        });
        When(x => x.Email.HasValue && x.Email.Value != null, () => {
            RuleFor(x => x.Email.Value!).EmailAddress().WithMessage("Invalid email format.");
        });
        When(x => x.DateOfBirth.HasValue, () => {
            RuleFor(x => x.DateOfBirth.Value)
                .LessThan(DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("Date of birth cannot be in the future.");
        });
        When(x => x.GuardianName.HasValue && x.GuardianName.Value != null, () => {
            RuleFor(x => x.GuardianName.Value!).MinimumLength(2).WithMessage("Guardian name must be at least 2 characters.");
        });
        When(x => x.GuardianPhone.HasValue && x.GuardianPhone.Value != null, () => {
            RuleFor(x => x.GuardianPhone.Value!).NotEmpty().WithMessage("Guardian phone is required.");
        });
        When(x => x.GuardianEmail.HasValue && x.GuardianEmail.Value != null, () => {
            RuleFor(x => x.GuardianEmail.Value!).EmailAddress().WithMessage("Invalid guardian email format.");
        });
    }
}

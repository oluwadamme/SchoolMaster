using FluentValidation;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Application.DTOs;

public record CreateStudentRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string StudentNumber,
    DateOnly DateOfBirth,
    Gender Gender,
    string GuardianName,
    string GuardianPhone,
    string GuardianEmail,
    string? MedicalNotes,
    string? PhotoUrl
);

public class CreateStudentRequestValidator : AbstractValidator<CreateStudentRequest>
{
    public CreateStudentRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("First name is required.");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("Last name is required.");
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required.").EmailAddress().WithMessage("Invalid email format.");
        RuleFor(x => x.StudentNumber).NotEmpty().WithMessage("Student number is required.");
        RuleFor(x => x.DateOfBirth).NotEmpty().WithMessage("Date of birth is required.").LessThan(DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Date of birth cannot be in the future.");
        RuleFor(x => x.Gender).IsInEnum().WithMessage("Invalid gender specified.");
        RuleFor(x => x.GuardianName).NotEmpty().WithMessage("Guardian name is required.");
        RuleFor(x => x.GuardianPhone).NotEmpty().WithMessage("Guardian phone is required.");
        RuleFor(x => x.GuardianEmail).NotEmpty().WithMessage("Guardian email is required.");
    }
}
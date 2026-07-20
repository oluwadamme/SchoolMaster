using FluentValidation;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Application.DTOs;

public record CreateStudentRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    DateOnly DateOfBirth,
    Gender Gender,
    string GuardianFirstName,
    string GuardianLastName,
    string GuardianPhone,
    string GuardianEmail,
    string? MedicalNotes,
    string? PhotoUrl,
    Guid ClassId
);

public class CreateStudentRequestValidator : AbstractValidator<CreateStudentRequest>
{
    public CreateStudentRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("First name is required.");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("Last name is required.");
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required.").EmailAddress().WithMessage("Invalid email format.");
        RuleFor(x => x.DateOfBirth).NotEmpty().WithMessage("Date of birth is required.").LessThan(DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Date of birth cannot be in the future.");
        RuleFor(x => x.Gender).IsInEnum().WithMessage("Invalid gender specified.");
        RuleFor(x => x.GuardianFirstName).NotEmpty().WithMessage("Guardian first name is required.");
        RuleFor(x => x.GuardianLastName).NotEmpty().WithMessage("Guardian last name is required.");
        RuleFor(x => x.GuardianPhone).NotEmpty().WithMessage("Guardian phone is required.");
        RuleFor(x => x.GuardianEmail).NotEmpty().WithMessage("Guardian email is required.");
        RuleFor(x => x.ClassId).NotEmpty().WithMessage("Class ID is required.");
    }
}


public record BulkEnrollStudentsRequest(IReadOnlyList<CreateStudentRequest> Students);

public class BulkEnrollStudentsRequestValidator : AbstractValidator<BulkEnrollStudentsRequest>
{
    private const int MaxBatchSize = 500;
    
    public BulkEnrollStudentsRequestValidator()
    {
        RuleFor(x => x.Students)
            .NotEmpty().WithMessage("At least one student record is required.")
            .Must(s => s.Count <= MaxBatchSize)
            .WithMessage($"A single import cannot exceed {MaxBatchSize} records.");
            
        RuleForEach(x => x.Students).SetValidator(new CreateStudentRequestValidator());
    }
}

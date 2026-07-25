namespace SchoolMaster.Application.DTOs;

using SchoolMaster.Domain.Enums;
using FluentValidation;

public record CreateStaffRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string Department,
    StaffRole StaffRole,
    EmploymentType EmploymentType
);

public class CreateStaffRequestValidator : AbstractValidator<CreateStaffRequest>
{
    public CreateStaffRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MinimumLength(2).WithMessage("First name must be at least 2 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MinimumLength(2).WithMessage("Last name must be at least 2 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.")
            .Matches("[!@#$%^&*]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.Department)
            .NotEmpty().WithMessage("Department is required.");
    }
}


public record BulkEnrollStaffRequest(IReadOnlyList<CreateStaffRequest> Staff);

public class BulkEnrollStaffRequestValidator : AbstractValidator<BulkEnrollStaffRequest>
{
    private const int MaxBatchSize = 500;
    public BulkEnrollStaffRequestValidator()
    {
        RuleFor(x => x.Staff)
            .NotEmpty().WithMessage("At least one staff record is required.")
            .Must(s => s.Count <= MaxBatchSize)
            .WithMessage($"A single import cannot exceed {MaxBatchSize} records.");
        RuleForEach(x => x.Staff).SetValidator(new CreateStaffRequestValidator());
    }
}


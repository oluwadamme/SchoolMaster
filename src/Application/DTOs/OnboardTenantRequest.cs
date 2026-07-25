using System;
using FluentValidation;
namespace SchoolMaster.Application.DTOs;

public class OnboardTenantRequest
{
    // Tenant info
    public required string SchoolName { get; set; }
    public required string Subdomain { get; set; }
    public required string ContactEmail { get; set; }
    public string SchoolCode { get; set; }

    // Admin info
    public required string AdminFirstName { get; set; }
    public required string AdminLastName { get; set; }
    public required string AdminEmail { get; set; }
    public required string AdminPassword { get; set; }
}
//the name of the validator class and reference to the class it's validating
public class OnboardTenantRequestValidator : AbstractValidator<OnboardTenantRequest>
{
    public OnboardTenantRequestValidator()


    {
        RuleFor(x => x.AdminFirstName)
            .NotEmpty().WithMessage("You must enter a first name.")
            .MinimumLength(2).WithMessage("First name must be at least 2 characters.");
        RuleFor(x => x.AdminLastName)
            .NotEmpty().WithMessage("You must enter a last name.")
            .MinimumLength(2).WithMessage("Last name must be at least 2 characters.");
        RuleFor(x => x.SchoolCode)
            .NotEmpty().WithMessage("Enter a school code.")
            .Length(3).WithMessage("School code must be 3 characters in all caps.")
            .Matches("^[A-Z]+$").WithMessage("School code can only contain uppercase letters.");
        RuleFor(x => x.AdminEmail)
            .NotEmpty().WithMessage("You must enter an email.")
            .EmailAddress().WithMessage("That is not a valid email format.");
        RuleFor(x => x.ContactEmail)
            .NotEmpty().WithMessage("You must enter an email.")
            .EmailAddress().WithMessage("That is not a valid email format.");

        RuleFor(x => x.AdminPassword)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain 1 uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain 1 number.")
            .Matches("[!@#$%^&*]").WithMessage("Password must contain 1 special character.");
        RuleFor(x => x.SchoolName).NotEmpty().WithMessage("You must enter a school name.");
        RuleFor(x => x.Subdomain)
            .NotEmpty().WithMessage("You must enter a subdomain.")
            .Matches("^[a-z0-9-]+$").WithMessage("Subdomain can only contain lowercase letters, numbers, and hyphens (no dots or spaces).")
            .NotEqual("www").WithMessage("This subdomain is reserved.")
            .NotEqual("api").WithMessage("This subdomain is reserved.");
    }
}
using System;
using FluentValidation;
namespace SchoolMaster.Application.DTOs;

public class OnboardTenantRequest
{
    // Tenant info
    public string SchoolName { get; set; }
    public string Subdomain { get; set; }
    public string ContactEmail { get; set; }

    // Admin info
    public string AdminFirstName { get; set; }
    public string AdminLastName { get; set; }
    public string AdminEmail { get; set; }
    public string AdminPassword { get; set; }
}
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
        RuleFor(x => x.AdminEmail)
            .NotEmpty().WithMessage("You must enter an email.")
            .EmailAddress().WithMessage("That is not a valid email format.");
        RuleFor(x => x.ContactEmail)
            .NotEmpty().WithMessage("You must enter an email.")
            .EmailAddress().WithMessage("That is not a valid email format.");

        RuleFor(x => x.AdminPassword)
            .NotEmpty()
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain 1 uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain 1 number.")
            .Matches("[!@#$%^&*]").WithMessage("Password must contain 1 special character.");
        RuleFor(x => x.SchoolName).NotEmpty().WithMessage("You must enter a school name.");
        RuleFor(x => x.Subdomain).NotEmpty().WithMessage("You must enter a subdomain.");

    }
}
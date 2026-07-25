using FluentValidation;
namespace SchoolMaster.Application.DTOs;
public class ResendOtpRequest
{
    public required string Email { get; set; }
}

public class ResendOtpRequestValidator : AbstractValidator<ResendOtpRequest>
{
    public ResendOtpRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("You must enter an email.")
            .EmailAddress().WithMessage("That is not a valid email format.");
    }
}
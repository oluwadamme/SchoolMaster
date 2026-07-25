using FluentValidation;
namespace SchoolMaster.Application.DTOs;
public class VerifyUserEmailRequest
{
    public required string Email { get; set; }
    public required string OtpToken { get; set; }
}

public class VerifyUserEmailRequestValidator : AbstractValidator<VerifyUserEmailRequest>
{
    public VerifyUserEmailRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("You must enter an email.")
            .EmailAddress().WithMessage("That is not a valid email format.");
        RuleFor(x => x.OtpToken)
            .NotEmpty().WithMessage("You must enter an otp token.");
    }
}

using FluentValidation;
namespace SchoolMaster.Application.DTOs;

public class ForgetPasswordRequest
{
    public string Email { get; set; }
}

public class ForgetPasswordRequestValidator : AbstractValidator<ForgetPasswordRequest>
{
    public ForgetPasswordRequestValidator()
    {
        RuleFor(user => user.Email)
            .NotEmpty().WithMessage("You must enter an email.")
            .EmailAddress().WithMessage("That is not a valid email format.");
    }
}
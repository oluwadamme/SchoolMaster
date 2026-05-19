using FluentValidation;
namespace SchoolMaster.Application.DTOs;

public class ResetPasswordRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string Otp { get; set; }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("You must enter an email.")
            .EmailAddress().WithMessage("That is not a valid email format.");
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("You must enter a password.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain 1 uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain 1 number.")
            .Matches("[!@#$%^&*]").WithMessage("Password must contain 1 special character.");
        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage("You must enter a token.");

    }
}
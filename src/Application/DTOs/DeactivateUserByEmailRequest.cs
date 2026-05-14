using FluentValidation;

namespace SchoolMaster.Application.DTOs;

public record DeactivateUserByEmailRequest(string Email);

public class DeactivateUserByEmailRequestValidator : AbstractValidator<DeactivateUserByEmailRequest>
{
    public DeactivateUserByEmailRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");
    }
}
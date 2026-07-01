using System;
using SchoolMaster.Domain.Enums;
using FluentValidation;

namespace SchoolMaster.Application.DTOs;

public record UpdateStaffRequest(
    Guid StaffId,
    Optional<string> FirstName = default,
    Optional<string> LastName = default,
    Optional<string> Email = default,
    Optional<string> Department = default,
    Optional<StaffRole> StaffRole = default,
    Optional<EmploymentType> EmploymentType = default
);

public class UpdateStaffRequestValidator : AbstractValidator<UpdateStaffRequest>
{
    public UpdateStaffRequestValidator()
    {
        RuleFor(x => x.StaffId).NotEmpty().WithMessage("StaffId is required.");
        When(x => x.FirstName != null, () => {
            RuleFor(x => x.FirstName!).MinimumLength(2).WithMessage("First name must be at least 2 characters.");
        });
        When(x => x.LastName != null, () => {
            RuleFor(x => x.LastName!).MinimumLength(2).WithMessage("Last name must be at least 2 characters.");
        });
        When(x => x.Email != null, () => {
            RuleFor(x => x.Email!).EmailAddress().WithMessage("Invalid email format.");
        });
    }
}

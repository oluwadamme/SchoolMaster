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
        // validating UpdateStaffRequest.StaffId and the rest
        
        RuleFor(x => x.StaffId).NotEmpty().WithMessage("StaffId is required.");
        When(x => x.FirstName.HasValue && x.FirstName.Value != null, () => {
            RuleFor(x => x.FirstName.Value!).MinimumLength(2).WithMessage("First name must be at least 2 characters.");
        });
        When(x => x.LastName.HasValue && x.LastName.Value != null, () => {
            RuleFor(x => x.LastName.Value!).MinimumLength(2).WithMessage("Last name must be at least 2 characters.");
        });
        When(x => x.Email.HasValue && x.Email.Value != null, () => {
            RuleFor(x => x.Email.Value!).EmailAddress().WithMessage("Invalid email format.");
        });
    }
}

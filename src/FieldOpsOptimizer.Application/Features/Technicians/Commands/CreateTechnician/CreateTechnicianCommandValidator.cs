using FluentValidation;

namespace FieldOpsOptimizer.Application.Features.Technicians.Commands.CreateTechnician;

public class CreateTechnicianCommandValidator : AbstractValidator<CreateTechnicianCommand>
{
    public CreateTechnicianCommandValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty()
            .WithMessage("Employee ID is required")
            .MaximumLength(50)
            .WithMessage("Employee ID must not exceed 50 characters");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("First name is required")
            .MaximumLength(100)
            .WithMessage("First name must not exceed 100 characters");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Last name is required")
            .MaximumLength(100)
            .WithMessage("Last name must not exceed 100 characters");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Email must be a valid email address")
            .MaximumLength(200)
            .WithMessage("Email must not exceed 200 characters");

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .WithMessage("Phone must not exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.HourlyRate)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Hourly rate must be greater than or equal to 0");

        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("Tenant ID is required");

        RuleFor(x => x.Skills)
            .NotNull()
            .WithMessage("Skills list cannot be null");

        RuleForEach(x => x.Skills)
            .NotEmpty()
            .WithMessage("Skill cannot be empty")
            .MaximumLength(100)
            .WithMessage("Skill must not exceed 100 characters");
    }
}

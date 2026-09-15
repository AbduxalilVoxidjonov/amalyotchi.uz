using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Directions;

public sealed class CreateDirectionCommandValidator : AbstractValidator<CreateDirectionCommand>
{
    public CreateDirectionCommandValidator()
    {
        RuleFor(x => x.DepartmentId).NotEmpty().WithMessage("Kafedra ko'rsatilmagan.");

        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(DirectionValidationRules.NameRequiredMessage)
            .Must(DirectionValidationRules.IsValidName).WithMessage(DirectionValidationRules.NameLengthMessage);

        RuleFor(x => x.Code)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(DirectionValidationRules.CodeRequiredMessage)
            .Must(DirectionValidationRules.IsValidCode).WithMessage(DirectionValidationRules.CodeFormatMessage);
    }
}

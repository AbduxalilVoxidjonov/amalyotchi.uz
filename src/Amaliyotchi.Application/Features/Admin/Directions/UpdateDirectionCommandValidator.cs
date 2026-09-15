using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Directions;

public sealed class UpdateDirectionCommandValidator : AbstractValidator<UpdateDirectionCommand>
{
    public UpdateDirectionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Yo'nalish ko'rsatilmagan.");

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

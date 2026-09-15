using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Directions;

public sealed class SetDirectionStatusCommandValidator : AbstractValidator<SetDirectionStatusCommand>
{
    public SetDirectionStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Yo'nalish ko'rsatilmagan.");
    }
}

using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Directions;

public sealed class DeleteDirectionCommandValidator : AbstractValidator<DeleteDirectionCommand>
{
    public DeleteDirectionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Yo'nalish ko'rsatilmagan.");
    }
}

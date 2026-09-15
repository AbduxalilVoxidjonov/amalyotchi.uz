using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Groups;

public sealed class SetGroupStatusCommandValidator : AbstractValidator<SetGroupStatusCommand>
{
    public SetGroupStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Guruh ko'rsatilmagan.");
    }
}

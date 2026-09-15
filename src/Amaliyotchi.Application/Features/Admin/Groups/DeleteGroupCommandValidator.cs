using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Groups;

public sealed class DeleteGroupCommandValidator : AbstractValidator<DeleteGroupCommand>
{
    public DeleteGroupCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Guruh ko'rsatilmagan.");
    }
}

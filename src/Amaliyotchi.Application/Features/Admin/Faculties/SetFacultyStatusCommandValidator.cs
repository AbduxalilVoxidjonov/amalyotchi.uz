using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Faculties;

public sealed class SetFacultyStatusCommandValidator : AbstractValidator<SetFacultyStatusCommand>
{
    public SetFacultyStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Fakultet ko'rsatilmagan.");
    }
}

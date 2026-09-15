using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Faculties;

public sealed class DeleteFacultyCommandValidator : AbstractValidator<DeleteFacultyCommand>
{
    public DeleteFacultyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Fakultet ko'rsatilmagan.");
    }
}

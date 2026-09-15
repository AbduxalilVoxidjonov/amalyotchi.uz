using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Departments;

public sealed class SetDepartmentStatusCommandValidator : AbstractValidator<SetDepartmentStatusCommand>
{
    public SetDepartmentStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Kafedra ko'rsatilmagan.");
    }
}

using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Departments;

public sealed class DeleteDepartmentCommandValidator : AbstractValidator<DeleteDepartmentCommand>
{
    public DeleteDepartmentCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Kafedra ko'rsatilmagan.");
    }
}

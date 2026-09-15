using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Departments;

public sealed class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Kafedra ko'rsatilmagan.");

        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(DepartmentValidationRules.NameRequiredMessage)
            .Must(DepartmentValidationRules.IsValidName).WithMessage(DepartmentValidationRules.NameLengthMessage);

        RuleFor(x => x.Code)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(DepartmentValidationRules.CodeRequiredMessage)
            .Must(DepartmentValidationRules.IsValidCode).WithMessage(DepartmentValidationRules.CodeFormatMessage);
    }
}

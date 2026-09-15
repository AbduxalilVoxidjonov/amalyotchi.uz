using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Faculties;

public sealed class CreateFacultyCommandValidator : AbstractValidator<CreateFacultyCommand>
{
    public CreateFacultyCommandValidator()
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(FacultyValidationRules.NameRequiredMessage)
            .Must(FacultyValidationRules.IsValidName).WithMessage(FacultyValidationRules.NameLengthMessage);

        RuleFor(x => x.Code)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(FacultyValidationRules.CodeRequiredMessage)
            .Must(FacultyValidationRules.IsValidCode).WithMessage(FacultyValidationRules.CodeFormatMessage);
    }
}

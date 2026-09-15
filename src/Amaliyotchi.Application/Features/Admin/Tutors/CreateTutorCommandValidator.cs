using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

public sealed class CreateTutorCommandValidator : AbstractValidator<CreateTutorCommand>
{
    public CreateTutorCommandValidator()
    {
        RuleFor(x => x.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(TutorValidationRules.FullNameRequiredMessage)
            .Must(TutorValidationRules.IsValidFullName).WithMessage(TutorValidationRules.FullNameLengthMessage);

        RuleFor(x => x.HemisId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(TutorValidationRules.HemisIdRequiredMessage)
            .Must(TutorValidationRules.IsValidHemisId).WithMessage(TutorValidationRules.HemisIdFormatMessage);

        RuleFor(x => x.Phone)
            .Must(TutorValidationRules.IsValidOptionalPhone).WithMessage(TutorValidationRules.PhoneFormatMessage);

        RuleFor(x => x.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(TutorValidationRules.PasswordRequiredMessage)
            .Must(TutorValidationRules.IsValidPassword).WithMessage(TutorValidationRules.PasswordLengthMessage);

        RuleFor(x => x.FacultyId).NotEmpty().WithMessage(TutorValidationRules.FacultyRequiredMessage);
    }
}

using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

public sealed class UpdateTutorCommandValidator : AbstractValidator<UpdateTutorCommand>
{
    public UpdateTutorCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(TutorValidationRules.IdRequiredMessage);

        RuleFor(x => x.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(TutorValidationRules.FullNameRequiredMessage)
            .Must(TutorValidationRules.IsValidFullName).WithMessage(TutorValidationRules.FullNameLengthMessage);

        RuleFor(x => x.Phone)
            .Must(TutorValidationRules.IsValidOptionalPhone).WithMessage(TutorValidationRules.PhoneFormatMessage);

        RuleFor(x => x.FacultyId).NotEmpty().WithMessage(TutorValidationRules.FacultyRequiredMessage);
    }
}

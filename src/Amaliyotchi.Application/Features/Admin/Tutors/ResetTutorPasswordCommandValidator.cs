using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

public sealed class ResetTutorPasswordCommandValidator : AbstractValidator<ResetTutorPasswordCommand>
{
    public ResetTutorPasswordCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(TutorValidationRules.IdRequiredMessage);

        RuleFor(x => x.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(TutorValidationRules.PasswordRequiredMessage)
            .Must(TutorValidationRules.IsValidPassword).WithMessage(TutorValidationRules.PasswordLengthMessage);
    }
}

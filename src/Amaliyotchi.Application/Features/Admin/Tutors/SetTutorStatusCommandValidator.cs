using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

public sealed class SetTutorStatusCommandValidator : AbstractValidator<SetTutorStatusCommand>
{
    public SetTutorStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(TutorValidationRules.IdRequiredMessage);
    }
}

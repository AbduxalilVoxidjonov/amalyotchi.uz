using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

public sealed class SetTutorScopesCommandValidator : AbstractValidator<SetTutorScopesCommand>
{
    public SetTutorScopesCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(TutorValidationRules.IdRequiredMessage);

        RuleFor(x => x.Scopes).NotNull().WithMessage("Ko'lamlar ro'yxati ko'rsatilmagan.");
        RuleForEach(x => x.Scopes).ChildRules(scope =>
        {
            scope.RuleFor(s => s.Level).IsInEnum().WithMessage("Ko'lam darajasi noto'g'ri.");
            scope.RuleFor(s => s.Id).NotEmpty().WithMessage("Tugun ko'rsatilmagan.");
        });
    }
}

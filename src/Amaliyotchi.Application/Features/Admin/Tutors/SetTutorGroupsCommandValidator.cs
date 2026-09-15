using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

public sealed class SetTutorGroupsCommandValidator : AbstractValidator<SetTutorGroupsCommand>
{
    public SetTutorGroupsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(TutorValidationRules.IdRequiredMessage);

        RuleFor(x => x.GroupIds).NotNull().WithMessage("Guruhlar ro'yxati ko'rsatilmagan.");
        RuleForEach(x => x.GroupIds).NotEmpty().WithMessage("Guruh ko'rsatilmagan.");
    }
}

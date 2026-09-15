using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Groups;

public sealed class CreateGroupCommandValidator : AbstractValidator<CreateGroupCommand>
{
    public CreateGroupCommandValidator()
    {
        RuleFor(x => x.DirectionId).NotEmpty().WithMessage("Yo'nalish ko'rsatilmagan.");

        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(GroupValidationRules.NameRequiredMessage)
            .Must(GroupValidationRules.IsValidName).WithMessage(GroupValidationRules.NameFormatMessage);

        RuleFor(x => x.Course)
            .Must(GroupValidationRules.IsValidCourse).WithMessage(GroupValidationRules.CourseRangeMessage);
    }
}

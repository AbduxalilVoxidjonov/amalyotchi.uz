using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Groups;

public sealed class UpdateGroupCommandValidator : AbstractValidator<UpdateGroupCommand>
{
    public UpdateGroupCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Guruh ko'rsatilmagan.");

        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(GroupValidationRules.NameRequiredMessage)
            .Must(GroupValidationRules.IsValidName).WithMessage(GroupValidationRules.NameFormatMessage);

        RuleFor(x => x.Course)
            .Must(GroupValidationRules.IsValidCourse).WithMessage(GroupValidationRules.CourseRangeMessage);
    }
}

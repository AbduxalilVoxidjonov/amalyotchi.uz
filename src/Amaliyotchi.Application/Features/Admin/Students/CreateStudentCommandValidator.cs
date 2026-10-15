using Amaliyotchi.Application.Features.Admin.Common;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary>Maydon qoidalari import bilan umumiy (<see cref="StudentFieldRules"/>); xato kalitlari — camelCase
/// (<see cref="CreateStudentFields"/>), forma maydon ostida ko'rsatadi.</summary>
public sealed class CreateStudentCommandValidator : AbstractValidator<CreateStudentCommand>
{
    public CreateStudentCommandValidator()
    {
        RuleFor(x => x.FullName).Custom((value, context) =>
        {
            if (StudentFieldRules.FullNameError(value, out _) is { } error)
                context.AddFailure(CreateStudentFields.FullName, error);
        });

        RuleFor(x => x.HemisId).Custom((value, context) =>
        {
            if (StudentFieldRules.HemisIdError(value, out _) is { } error)
                context.AddFailure(CreateStudentFields.HemisId, error);
        });

        RuleFor(x => x.GroupId)
            .NotEmpty().WithMessage(StudentImportMessages.GroupRequiredMessage)
            .OverridePropertyName(CreateStudentFields.GroupId);

        RuleFor(x => x.PhoneNumber).Custom((value, context) =>
        {
            if (StudentFieldRules.PhoneError(value, out _) is { } error)
                context.AddFailure(CreateStudentFields.PhoneNumber, error);
        });

        // v3.34: ixtiyoriy vaqtinchalik parol — berilgan bo'lsa admin paroli qoidalari (8–128 belgi).
        When(x => x.Password is not null, () =>
        {
            RuleFor(x => x.Password!)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(AccountValidationRules.PasswordRequiredMessage)
                .Must(AccountValidationRules.IsValidPassword).WithMessage(AccountValidationRules.PasswordLengthMessage)
                .OverridePropertyName(CreateStudentFields.Password);
        });
    }
}

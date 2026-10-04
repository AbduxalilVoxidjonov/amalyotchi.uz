using Amaliyotchi.Application.Features.Admin.Common;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary>Parol qoidalari admin paroli bilan bir xil (<see cref="AccountValidationRules"/>).</summary>
public sealed class SetStudentPasswordCommandValidator : AbstractValidator<SetStudentPasswordCommand>
{
    public SetStudentPasswordCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Talaba ko'rsatilmagan.");

        RuleFor(x => x.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(AccountValidationRules.PasswordRequiredMessage)
            .Must(AccountValidationRules.IsValidPassword).WithMessage(AccountValidationRules.PasswordLengthMessage);
    }
}

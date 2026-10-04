using Amaliyotchi.Application.Features.Admin.Common;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Auth.ChangePassword;

/// <summary>Yangi parol qoidalari — admin/talaba paroli o'rnatilgandagi bilan bir xil (<see cref="AccountValidationRules"/>).</summary>
public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Joriy parolni kiriting.");

        RuleFor(x => x.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Yangi parolni kiriting.")
            .Must(AccountValidationRules.IsValidPassword).WithMessage(AccountValidationRules.PasswordLengthMessage)
            .NotEqual(x => x.CurrentPassword).WithMessage("Yangi parol joriy paroldan farq qilishi kerak.");
    }
}

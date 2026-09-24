using Amaliyotchi.Application.Features.Admin.Tutors;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Auth.ChangePassword;

/// <summary>Yangi parol qoidalari — tyutor/talaba paroli o'rnatilgandagi bilan bir xil (<see cref="TutorValidationRules"/>).</summary>
public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Joriy parolni kiriting.");

        RuleFor(x => x.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Yangi parolni kiriting.")
            .Must(TutorValidationRules.IsValidPassword).WithMessage(TutorValidationRules.PasswordLengthMessage)
            .NotEqual(x => x.CurrentPassword).WithMessage("Yangi parol joriy paroldan farq qilishi kerak.");
    }
}

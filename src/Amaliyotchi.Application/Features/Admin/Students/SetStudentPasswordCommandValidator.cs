using Amaliyotchi.Application.Features.Admin.Tutors;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary>Parol qoidalari tyutor paroli bilan bir xil (<see cref="TutorValidationRules"/>).</summary>
public sealed class SetStudentPasswordCommandValidator : AbstractValidator<SetStudentPasswordCommand>
{
    public SetStudentPasswordCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Talaba ko'rsatilmagan.");

        RuleFor(x => x.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(TutorValidationRules.PasswordRequiredMessage)
            .Must(TutorValidationRules.IsValidPassword).WithMessage(TutorValidationRules.PasswordLengthMessage);
    }
}

using FluentValidation;

namespace Amaliyotchi.Application.Features.Auth.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Telefon raqamini kiriting.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Parolni kiriting.")
            .MinimumLength(8).WithMessage("Parol kamida 8 ta belgidan iborat bo'lishi kerak.");
    }
}

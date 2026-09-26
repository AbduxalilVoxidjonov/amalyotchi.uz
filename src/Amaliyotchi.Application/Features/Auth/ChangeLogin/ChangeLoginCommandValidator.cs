using FluentValidation;

namespace Amaliyotchi.Application.Features.Auth.ChangeLogin;

public sealed class ChangeLoginCommandValidator : AbstractValidator<ChangeLoginCommand>
{
    public ChangeLoginCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Joriy parolni kiriting.");

        RuleFor(x => x.NewLogin)
            .Custom((value, context) =>
            {
                if (LoginAvailability.FormatError(value, out _) is { } error)
                    context.AddFailure(nameof(ChangeLoginCommand.NewLogin), error);
            });
    }
}

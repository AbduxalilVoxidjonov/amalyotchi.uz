using FluentValidation;

namespace Amaliyotchi.Application.Features.Auth.Telegram;

public sealed class TelegramLoginCommandValidator : AbstractValidator<TelegramLoginCommand>
{
    /// <summary>Telegram initData odatda 300–800 belgi; 8 KB — himoya chegarasi.</summary>
    private const int MaxLength = 8 * 1024;

    public TelegramLoginCommandValidator()
    {
        RuleFor(x => x.InitData)
            .NotEmpty().WithMessage("initData bo'sh bo'lishi mumkin emas.")
            .MaximumLength(MaxLength).WithMessage("initData juda uzun.");
    }
}

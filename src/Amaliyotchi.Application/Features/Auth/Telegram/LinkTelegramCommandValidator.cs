using FluentValidation;

namespace Amaliyotchi.Application.Features.Auth.Telegram;

/// <summary>Qoidalar <see cref="TelegramLoginCommandValidator"/> (initData) va <c>LoginCommandValidator</c>
/// (HEMIS ID, parol) bilan bir xil.</summary>
public sealed class LinkTelegramCommandValidator : AbstractValidator<LinkTelegramCommand>
{
    public LinkTelegramCommandValidator()
    {
        RuleFor(x => x.InitData)
            .NotEmpty().WithMessage("initData bo'sh bo'lishi mumkin emas.")
            .MaximumLength(TelegramLoginCommandValidator.MaxLength).WithMessage("initData juda uzun.");

        RuleFor(x => x.HemisId)
            .NotEmpty().WithMessage("HEMIS ID ni kiriting.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Parolni kiriting.")
            .MinimumLength(8).WithMessage("Parol kamida 8 ta belgidan iborat bo'lishi kerak.");
    }
}

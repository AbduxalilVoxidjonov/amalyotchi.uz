using Amaliyotchi.Application.Common.Models;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Common;

/// <summary>Sahifalash parametrlarining umumiy tekshiruvi. <see cref="PagedQuery"/> noto'g'ri qiymatlarni o'zi
/// standartga tushiradi, shuning uchun bu yerda faqat qidiruv matnining uzunligi cheklanadi
/// (juda uzun <c>q</c> — LIKE naqshini bema'ni qilib yuboradi) va sahifa chegaralari hujjatlanadi.</summary>
public abstract class PagedQueryValidator<T> : AbstractValidator<T>
    where T : PagedQuery
{
    public const int MaxQueryLength = 100;

    protected PagedQueryValidator()
    {
        RuleFor(x => x.Q)
            .MaximumLength(MaxQueryLength).WithMessage($"Qidiruv matni {MaxQueryLength} belgidan oshmasligi kerak.")
            .When(x => x.Q is not null);

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Sahifa raqami 1 dan kichik bo'lishi mumkin emas.");

        RuleFor(x => x.PageSize)
            .Must((query, size) => size >= 1 && size <= query.MaxPageSizeLimit)
            .WithMessage(query => $"Sahifa hajmi 1–{query.MaxPageSizeLimit} oralig'ida bo'lishi kerak.");
    }
}

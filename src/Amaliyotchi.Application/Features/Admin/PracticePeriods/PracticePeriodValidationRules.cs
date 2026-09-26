using System.Globalization;
using System.Linq.Expressions;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.PracticePeriods;

/// <summary>Create/Update validatorlari uchun umumiy qoidalar.</summary>
internal static class PracticePeriodValidationRules
{
    public const string NameRequiredMessage = "Davr nomini kiriting.";
    public static readonly string NameLengthMessage = $"Davr nomi {PracticePeriod.NameMaxLength} belgidan oshmasligi kerak.";
    public const string StartRequiredMessage = "Boshlanish sanasini kiriting.";
    public const string EndRequiredMessage = "Tugash sanasini kiriting.";
    public const string EndBeforeStartMessage = "Tugash sanasi boshlanish sanasidan oldin bo'lishi mumkin emas.";
    public const string GroupIdsRequiredMessage = "Kamida bitta guruh tanlang.";
    public const string GroupIdEmptyMessage = "Guruh identifikatori bo'sh bo'lishi mumkin emas.";
    public const string TimeFormatMessage = "Vaqtni HH:mm formatida kiriting.";
    public const string DailyEndBeforeStartMessage = "Ish tugash vaqti boshlanish vaqtidan keyin bo'lishi kerak.";
    public const string WorkDaysRequiredMessage = "Kamida bitta ish kunini tanlang.";
    public const string WorkDaysFormatMessage =
        "Ish kunlari 1 (Dushanba) dan 7 (Yakshanba) gacha bo'lgan raqamlar ro'yxati bo'lishi kerak.";

    /// <summary>Xato kalitlari — FluentValidation property nomlari bilan bir xil (PascalCase).</summary>
    public const string DailyStartKey = "DailyStart";
    public const string DailyEndKey = "DailyEnd";
    public const string WorkDaysKey = "WorkDays";

    private static readonly string[] TimeFormats = ["HH:mm", "H:mm"];

    /// <summary>"HH:mm" (Toshkent vaqti) → <see cref="TimeOnly"/>.</summary>
    public static bool TryParseTime(string? value, out TimeOnly time)
        => TimeOnly.TryParseExact(value?.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    /// <summary>"1,2,3,4,5" (1=Du … 7=Ya) → bitmask. Noto'g'ri format → <c>null</c>; bo'sh → <see cref="WorkDays.None"/>.</summary>
    public static WorkDays? TryParseWorkDays(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return WorkDays.None;
        try
        {
            return WorkDaysExtensions.Parse(csv);
        }
        catch (DomainException)
        {
            return null;
        }
    }

    /// <summary>Ixtiyoriy <c>dailyStart</c>/<c>dailyEnd</c>/<c>workDays</c> (null → yuborilmagan). Ikkala vaqt berilsa —
    /// <c>dailyEnd &gt; dailyStart</c> (<c>errors.DailyEnd</c>). Check-in oynasi sig'ishi handler'da tekshiriladi.</summary>
    public static void ApplySchedule<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string?>> dailyStart,
        Expression<Func<T, string?>> dailyEnd,
        Expression<Func<T, string?>> workDays)
    {
        var startOf = dailyStart.Compile();

        validator.RuleFor(dailyStart)
            .Must(v => v is null || TryParseTime(v, out _)).WithMessage(TimeFormatMessage);

        validator.RuleFor(dailyEnd)
            .Cascade(CascadeMode.Stop)
            .Must(v => v is null || TryParseTime(v, out _)).WithMessage(TimeFormatMessage)
            .Must((x, v) => v is null
                            || !TryParseTime(startOf(x), out var start)
                            || !TryParseTime(v, out var end)
                            || end > start)
            .WithMessage(DailyEndBeforeStartMessage);

        validator.RuleFor(workDays)
            .Cascade(CascadeMode.Stop)
            .Must(v => v is null || TryParseWorkDays(v) is not null).WithMessage(WorkDaysFormatMessage)
            .Must(v => v is null || TryParseWorkDays(v) != WorkDays.None).WithMessage(WorkDaysRequiredMessage);
    }

    public static void ApplyNameAndDates<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string>> name,
        Expression<Func<T, DateOnly>> start,
        Expression<Func<T, DateOnly>> end)
    {
        var startOf = start.Compile();
        validator.RuleFor(name)
            .Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage(NameRequiredMessage)
            .Must(n => n.Trim().Length <= PracticePeriod.NameMaxLength).WithMessage(NameLengthMessage);

        validator.RuleFor(start).NotEmpty().WithMessage(StartRequiredMessage);
        validator.RuleFor(end)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(EndRequiredMessage)
            .Must((x, e) => e >= startOf(x)).WithMessage(EndBeforeStartMessage);
    }
}

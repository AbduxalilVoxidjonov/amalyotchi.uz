using System.Linq.Expressions;
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

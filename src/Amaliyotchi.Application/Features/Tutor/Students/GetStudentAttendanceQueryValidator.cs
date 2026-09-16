using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Tutor.Students;

/// <summary><c>?from=&amp;to=</c> oralig'i: teskari bo'lmasin va juda uzun bo'lmasin.
/// Berilmagan chegara handler'da davr sanalaridan olinadi — bu yerda bugungi kun bilan taxminlanadi.</summary>
public sealed class GetStudentAttendanceQueryValidator : AbstractValidator<GetStudentAttendanceQuery>
{
    /// <summary>Bir so'rovda qaytariladigan maksimal kunlar oralig'i.</summary>
    public const int MaxRangeDays = 400;

    public GetStudentAttendanceQueryValidator(IClock clock)
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From!.Value)
            .When(x => x.From is not null && x.To is not null)
            .WithMessage("'from' sanasi 'to' sanasidan keyin bo'lishi mumkin emas.");

        RuleFor(x => x)
            .Must(query => SpanDays(query, clock.LocalToday()) <= MaxRangeDays)
            .When(query => query.From is not null || query.To is not null)
            .WithMessage($"So'ralgan oraliq {MaxRangeDays} kundan uzun bo'lmasligi kerak.")
            .OverridePropertyName(nameof(GetStudentAttendanceQuery.From));
    }

    private static int SpanDays(GetStudentAttendanceQuery query, DateOnly today)
    {
        var from = query.From ?? today;
        var to = query.To ?? today;
        return to.DayNumber - from.DayNumber;
    }
}

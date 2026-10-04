using FluentValidation;

namespace Amaliyotchi.Application.Features.Supervision.Calendar;

public sealed class GetAttendanceCalendarQueryValidator : AbstractValidator<GetAttendanceCalendarQuery>
{
    public GetAttendanceCalendarQueryValidator()
    {
        RuleFor(x => x.Month)
            .Must(m => GetAttendanceCalendarQuery.TryParseMonth(m, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Month))
            .WithMessage("Oy YYYY-MM ko'rinishida bo'lishi kerak (masalan, 2026-10).");
    }
}

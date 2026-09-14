using FluentValidation;

namespace Amaliyotchi.Application.Features.Tutor.Calendar;

public sealed class GetTutorCalendarQueryValidator : AbstractValidator<GetTutorCalendarQuery>
{
    public GetTutorCalendarQueryValidator()
    {
        RuleFor(x => x.Month)
            .Must(m => GetTutorCalendarQuery.TryParseMonth(m, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Month))
            .WithMessage("Oy YYYY-MM ko'rinishida bo'lishi kerak (masalan, 2026-10).");
    }
}

using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using FluentValidation;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.PracticePeriods;

/// <summary><c>PUT /api/admin/practice-periods/{id}</c>: <c>{ name, startDate, endDate, dailyStart?, dailyEnd?,
/// workDays? }</c> → 200 <see cref="PracticePeriodDetail"/>. <see cref="Id"/> route'dan. Topilmasa → 404; yopilgan → 409;
/// faol davrda <c>startDate</c> o'zgarsa yoki yangi <c>endDate</c> bugundan oldin bo'lsa → 400. Sanalar o'zgarsa —
/// ustma-ust tushish qayta tekshiriladi (409). <c>dailyStart</c>/<c>dailyEnd</c> ("HH:mm") va <c>workDays</c>
/// ("1,2,3,4,5") ixtiyoriy — null → o'zgarmaydi; davrning kechikish/oyna/avto-yopish daqiqalari saqlanadi, check-in
/// oynasi sig'masa → 400 <c>errors.DailyEnd</c>. Sanalar yoki <c>workDays</c> o'zgarsa <c>requiredDays</c> qayta
/// hisoblanadi (bayramlarsiz). Davomat yozuvlari o'zgarmaydi.</summary>
public sealed record UpdatePracticePeriodCommand(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string? DailyStart = null,
    string? DailyEnd = null,
    string? WorkDays = null)
    : IRequest<PracticePeriodDetail>;

public sealed class UpdatePracticePeriodCommandValidator : AbstractValidator<UpdatePracticePeriodCommand>
{
    public UpdatePracticePeriodCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Davr ko'rsatilmagan.");
        PracticePeriodValidationRules.ApplyNameAndDates(this, x => x.Name, x => x.StartDate, x => x.EndDate);
        PracticePeriodValidationRules.ApplySchedule(this, x => x.DailyStart, x => x.DailyEnd, x => x.WorkDays);
    }
}

internal sealed class UpdatePracticePeriodCommandHandler(IApplicationDbContext db, IAuditWriter audit, IClock clock)
    : IRequestHandler<UpdatePracticePeriodCommand, PracticePeriodDetail>
{
    public async Task<PracticePeriodDetail> Handle(UpdatePracticePeriodCommand request, CancellationToken cancellationToken)
    {
        var period = await PracticePeriodQueries.FindTrackedAsync(db, request.Id, cancellationToken);
        if (!period.IsOpen)
            throw new ConflictException("Yopilgan davrni tahrirlab bo'lmaydi.");

        var dailyStart = PracticePeriodQueries.TimeOr(request.DailyStart, period.DailyStart);
        var dailyEnd = PracticePeriodQueries.TimeOr(request.DailyEnd, period.DailyEnd);
        var workDays = PracticePeriodQueries.WorkDaysOr(request.WorkDays, period.WorkDays);

        var datesChanged = period.StartDate != request.StartDate || period.EndDate != request.EndDate;
        var hoursChanged = dailyStart != period.DailyStart || dailyEnd != period.DailyEnd;
        var workDaysChanged = workDays != period.WorkDays;

        if (hoursChanged)
        {
            // Bittasi yuborilgan bo'lsa ham, davrning boshqa qiymati bilan birga tekshiriladi → 400 errors.DailyEnd.
            PracticePeriodQueries.BuildRules(
                dailyStart, dailyEnd, period.LateToleranceMinutes, period.CheckInWindowMinutes,
                period.CheckoutGraceMinutes, CheckInRules.Default.MinAccuracyM);
        }

        var requiredDays = period.RequiredDays;
        if (datesChanged || workDaysChanged)
        {
            requiredDays = await PracticePeriodQueries.CountRequiredDaysAsync(
                db, request.StartDate, request.EndDate, workDays, cancellationToken);
        }

        if (hoursChanged || workDaysChanged)
            period.ChangeSchedule(dailyStart, dailyEnd, workDays, requiredDays);

        if (datesChanged)
        {
            var today = clock.LocalToday();

            // Domain qoidalari (faol davr boshlanishi, bugundan oldingi tugash) ustma-ust tekshiruvidan oldin — 400 ustun.
            period.Reschedule(request.StartDate, request.EndDate, requiredDays, today);

            await PracticePeriodQueries.EnsureNoOverlapAsync(
                db, period.Groups.Select(g => g.StudentGroupId).ToList(),
                request.StartDate, request.EndDate, period.Id, cancellationToken);
        }

        period.Rename(request.Name);

        await audit.WriteAsync(
            AuditAction.PracticePeriodUpdated, nameof(PracticePeriod), period.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return await PracticePeriodQueries.LoadDetailAsync(db, clock, period.Id, cancellationToken);
    }
}

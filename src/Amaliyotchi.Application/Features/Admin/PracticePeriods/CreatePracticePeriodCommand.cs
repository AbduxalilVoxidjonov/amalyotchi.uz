using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using FluentValidation;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.PracticePeriods;

/// <summary><c>POST /api/admin/practice-periods</c>: <c>{ name, startDate, endDate, groupIds, dailyStart?, dailyEnd?,
/// workDays? }</c> → 201 <see cref="PracticePeriodDetail"/>. <c>dailyStart</c>/<c>dailyEnd</c> — "HH:mm" (Toshkent),
/// <c>workDays</c> — "1,2,3,4,5" (1=Du … 7=Ya, kamida bitta); yuborilmasa (null) — standart 09:00/17:00 va global
/// <c>workDays</c> sozlamasi. Kechikish/check-in oynasi/avto-yopish daqiqalari va <c>dailyReportRequired</c> global
/// sozlamalardan nusxalanadi; tugash boshlanishdan oldin → 400 <c>errors.DailyEnd</c> (check-in oynasi kundan
/// uzun bo'lsa — ish tugashigacha avtomatik qisqaradi).
/// <c>requiredDays</c> — oraliqdagi ish kunlari (bayramlarsiz). O'quv yili — joriy (faol), yo'q → 400.
/// Guruh topilmasa/faol emas → 400 <c>errors.GroupIds</c>; sanalari kesishadigan boshqa ochiq davrda bo'lsa → 409.
/// Saqlanadigan holat darhol <c>Active</c> (ochiq); <c>planned</c> ko'rinishi sanadan hisoblanadi.</summary>
public sealed record CreatePracticePeriodCommand(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<Guid> GroupIds,
    string? DailyStart = null,
    string? DailyEnd = null,
    string? WorkDays = null)
    : IRequest<PracticePeriodDetail>;

public sealed class CreatePracticePeriodCommandValidator : AbstractValidator<CreatePracticePeriodCommand>
{
    public CreatePracticePeriodCommandValidator()
    {
        PracticePeriodValidationRules.ApplyNameAndDates(this, x => x.Name, x => x.StartDate, x => x.EndDate);
        PracticePeriodValidationRules.ApplySchedule(this, x => x.DailyStart, x => x.DailyEnd, x => x.WorkDays);

        RuleFor(x => x.GroupIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(PracticePeriodValidationRules.GroupIdsRequiredMessage)
            .NotEmpty().WithMessage(PracticePeriodValidationRules.GroupIdsRequiredMessage)
            .Must(ids => ids.All(id => id != Guid.Empty)).WithMessage(PracticePeriodValidationRules.GroupIdEmptyMessage);
    }
}

internal sealed class CreatePracticePeriodCommandHandler(
    IApplicationDbContext db, IAuditWriter audit, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<CreatePracticePeriodCommand, PracticePeriodDetail>
{
    public async Task<PracticePeriodDetail> Handle(CreatePracticePeriodCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var groupIds = request.GroupIds.Distinct().ToList();

        await PracticePeriodQueries.EnsureGroupsAttachableAsync(db, groupIds, cancellationToken);
        var academicYearId = await PracticePeriodQueries.CurrentAcademicYearIdAsync(db, cancellationToken);
        await PracticePeriodQueries.EnsureNoOverlapAsync(
            db, groupIds, request.StartDate, request.EndDate, excludePeriodId: null, cancellationToken);

        var defaults = await PracticePeriodQueries.LoadDefaultsAsync(db, cancellationToken);
        var d = defaults.Rules;
        var rules = PracticePeriodQueries.BuildRules(
            PracticePeriodQueries.TimeOr(request.DailyStart, d.DailyStart),
            PracticePeriodQueries.TimeOr(request.DailyEnd, d.DailyEnd),
            d.LateToleranceMinutes, d.CheckInWindowMinutes, d.CheckoutGraceMinutes, d.MinAccuracyM);
        var workDays = PracticePeriodQueries.WorkDaysOr(request.WorkDays, defaults.WorkDays);

        var requiredDays = await PracticePeriodQueries.CountRequiredDaysAsync(
            db, request.StartDate, request.EndDate, workDays, cancellationToken);

        var period = PracticePeriod.Create(
            request.Name, academicYearId, request.StartDate, request.EndDate, userId,
            rules, workDays, requiredDays, defaults.DailyReportRequired);
        foreach (var groupId in groupIds)
            period.AttachGroup(groupId);

        // Ochiq davr (saqlanadigan holat Active = "yopilmagan"). Qaysi davr qayerda ishlatilishi sanadan hisoblanadi
        // (PeriodSelection: davom etayotgan / oxirgi tugagan / kelgusi) — boshlanmagan davrda check-in rad etiladi,
        // ariza esa oldindan berilishi mumkin.
        period.Activate();
        db.PracticePeriods.Add(period);

        await audit.WriteAsync(
            AuditAction.PracticePeriodCreated, nameof(PracticePeriod), period.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return await PracticePeriodQueries.LoadDetailAsync(db, clock, period.Id, cancellationToken);
    }
}

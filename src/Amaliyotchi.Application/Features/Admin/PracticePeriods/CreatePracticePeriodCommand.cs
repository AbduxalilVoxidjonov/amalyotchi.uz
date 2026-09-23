using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using FluentValidation;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.PracticePeriods;

/// <summary><c>POST /api/admin/practice-periods</c>: <c>{ name, startDate, endDate, groupIds }</c> → 201
/// <see cref="PracticePeriodDetail"/>. Vaqt qoidalari, ish kunlari va <c>dailyReportRequired</c> global sozlamalardan
/// nusxalanadi; <c>requiredDays</c> — oraliqdagi ish kunlari (bayramlarsiz). O'quv yili — joriy (faol), yo'q → 400.
/// Guruh topilmasa/faol emas → 400 <c>errors.GroupIds</c>; sanalari kesishadigan boshqa ochiq davrda bo'lsa → 409.
/// Saqlanadigan holat darhol <c>Active</c> (ochiq); <c>planned</c> ko'rinishi sanadan hisoblanadi.</summary>
public sealed record CreatePracticePeriodCommand(string Name, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<Guid> GroupIds)
    : IRequest<PracticePeriodDetail>;

public sealed class CreatePracticePeriodCommandValidator : AbstractValidator<CreatePracticePeriodCommand>
{
    public CreatePracticePeriodCommandValidator()
    {
        PracticePeriodValidationRules.ApplyNameAndDates(this, x => x.Name, x => x.StartDate, x => x.EndDate);

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
        var requiredDays = await PracticePeriodQueries.CountRequiredDaysAsync(
            db, request.StartDate, request.EndDate, defaults.WorkDays, cancellationToken);

        var period = PracticePeriod.Create(
            request.Name, academicYearId, request.StartDate, request.EndDate, userId,
            defaults.Rules, defaults.WorkDays, requiredDays, defaults.DailyReportRequired);
        foreach (var groupId in groupIds)
            period.AttachGroup(groupId);

        // Ochiq davr: student/tyutor oqimlari (PeriodLookup, check-in, ariza) saqlangan Active holatiga tayanadi,
        // boshlanmagan davrda check-in esa "periodNotStarted" bilan to'xtatiladi.
        period.Activate();
        db.PracticePeriods.Add(period);

        await audit.WriteAsync(
            AuditAction.PracticePeriodCreated, nameof(PracticePeriod), period.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return await PracticePeriodQueries.LoadDetailAsync(db, clock, period.Id, cancellationToken);
    }
}

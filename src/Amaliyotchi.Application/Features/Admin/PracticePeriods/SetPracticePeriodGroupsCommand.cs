using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using FluentValidation;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.PracticePeriods;

/// <summary><c>PUT /api/admin/practice-periods/{id}/groups</c>: <c>{ groupIds }</c> — to'liq ro'yxat (set semantikasi) →
/// 200 <see cref="PracticePeriodDetail"/>. Yopilgan davr → 409. Yangi qo'shilayotgan guruh topilmasa/faol emas → 400;
/// sanalari kesishadigan boshqa ochiq davrda bo'lsa → 409. Ajratilayotgan guruh talabalarining shu davrda davomat
/// yozuvi bo'lsa → 409. Bo'sh ro'yxat ruxsat etiladi (hamma guruhni ajratish — davomat bo'lmasa).</summary>
public sealed record SetPracticePeriodGroupsCommand(Guid Id, IReadOnlyList<Guid> GroupIds) : IRequest<PracticePeriodDetail>;

public sealed class SetPracticePeriodGroupsCommandValidator : AbstractValidator<SetPracticePeriodGroupsCommand>
{
    public SetPracticePeriodGroupsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Davr ko'rsatilmagan.");
        RuleFor(x => x.GroupIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Guruhlar ro'yxati berilmagan.")
            .Must(ids => ids.All(id => id != Guid.Empty)).WithMessage(PracticePeriodValidationRules.GroupIdEmptyMessage);
    }
}

internal sealed class SetPracticePeriodGroupsCommandHandler(IApplicationDbContext db, IAuditWriter audit, IClock clock)
    : IRequestHandler<SetPracticePeriodGroupsCommand, PracticePeriodDetail>
{
    public async Task<PracticePeriodDetail> Handle(SetPracticePeriodGroupsCommand request, CancellationToken cancellationToken)
    {
        var period = await PracticePeriodQueries.FindTrackedAsync(db, request.Id, cancellationToken);
        if (!period.IsOpen)
            throw new ConflictException("Yopilgan davrning guruhlarini o'zgartirib bo'lmaydi.");

        var target = request.GroupIds.Distinct().ToHashSet();
        var current = period.Groups.Select(g => g.StudentGroupId).ToHashSet();
        var toAdd = target.Where(id => !current.Contains(id)).ToList();
        var toRemove = current.Where(id => !target.Contains(id)).ToList();

        if (toAdd.Count == 0 && toRemove.Count == 0)
            return await PracticePeriodQueries.LoadDetailAsync(db, clock, period.Id, cancellationToken);

        var withAttendance = await PracticePeriodQueries.GroupsWithAttendanceAsync(db, period.Id, toRemove, cancellationToken);
        if (withAttendance.Count > 0)
        {
            var names = string.Join(", ", withAttendance.Values.Order());
            throw new ConflictException(
                $"Quyidagi guruhlar talabalarining shu davrda davomat yozuvlari bor — ularni ajratib bo'lmaydi: {names}");
        }

        await PracticePeriodQueries.EnsureGroupsAttachableAsync(db, toAdd, cancellationToken);
        await PracticePeriodQueries.EnsureNoOverlapAsync(
            db, toAdd, period.StartDate, period.EndDate, period.Id, cancellationToken);

        foreach (var groupId in toRemove)
            period.DetachGroup(groupId, hasAttendanceRecords: false);
        foreach (var groupId in toAdd)
            db.PracticePeriodGroups.Add(period.AttachGroup(groupId));

        var changes = System.Text.Json.JsonSerializer.Serialize(new { added = toAdd, removed = toRemove });
        await audit.WriteAsync(
            AuditAction.PracticePeriodGroupsChanged, nameof(PracticePeriod), period.Id.ToString(), changes,
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return await PracticePeriodQueries.LoadDetailAsync(db, clock, period.Id, cancellationToken);
    }
}

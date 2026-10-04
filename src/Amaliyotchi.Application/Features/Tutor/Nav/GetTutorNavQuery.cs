using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Application.Features.Tutor.Diaries;
using Amaliyotchi.Application.Features.Tutor.Faces;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Nav;

/// <summary>Sidebar badge'lari (tyutor ko'lami bo'yicha), har biri tegishli sahifaning sukut holatidagi soni:
/// <list type="bullet">
/// <item><paramref name="Today"/> — <c>GET /api/tutor/today</c> → <c>stats.total</c> (bugungi jadval qatorlari = ko'lamdagi talabalar);</item>
/// <item><paramref name="Applications"/> — <c>GET /api/tutor/applications</c> → <c>counts.submitted</c> (ko'rib chiqilishi kerak);</item>
/// <item><paramref name="Students"/> — <c>GET /api/tutor/students</c> ro'yxati uzunligi;</item>
/// <item><paramref name="Diaries"/> — tekshirilmagan kundaliklar (<see cref="DiaryQueue.Unreviewed"/>: submitted/seen);</item>
/// <item><paramref name="PendingFaceEnrollments"/> — <c>GET /api/tutor/face-enrollments</c> sukut (pending) ro'yxati uzunligi.</item>
/// </list></summary>
public sealed record TutorNavCounts(int Today, int Applications, int Students, int Diaries, int PendingFaceEnrollments);

/// <summary>Header crumb: <paramref name="Groups"/> — ko'lamdagi guruh nomlari (ordinal tartibda);
/// <paramref name="PeriodName"/> — guruhlar uchun joriy davr nomi yoki <c>null</c>.</summary>
public sealed record TutorNavContext(IReadOnlyList<string> Groups, string? PeriodName);

public sealed record TutorNavDto(TutorNavCounts Counts, TutorNavContext Context);

/// <summary><c>GET /api/tutor/nav</c> — har sahifada chaqiriladi: faqat COUNT'lar va ikki yengil ro'yxat (guruh nomlari,
/// guruhlarga biriktirilgan davrlar), N+1 yo'q.</summary>
public sealed record GetTutorNavQuery : IRequest<TutorNavDto>;

internal sealed class GetTutorNavQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock)
    : IRequestHandler<GetTutorNavQuery, TutorNavDto>
{
    public async Task<TutorNavDto> Handle(GetTutorNavQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);

        // "Bugun" jadvali ko'lamdagi har talabaga bitta qator beradi (stats.total == ko'lamdagi talabalar) —
        // shuning uchun ikkala badge bitta COUNT'dan.
        var students = await db.CountScopedStudentsAsync(scope, cancellationToken);

        var applications = await db.PracticeApplications.AsNoTracking().InScope(scope)
            .CountAsync(a => a.Status == ApplicationStatus.Submitted, cancellationToken);

        // Ro'yxat (SelectTutorEntriesAsync) talaba profili bilan birlashtiriladi — hisob ham xuddi shunday.
        var diaries = await db.DiaryEntries.AsNoTracking().InScope(scope)
            .Where(DiaryQueue.Unreviewed)
            .Join(db.StudentProfiles, d => d.StudentUserId, p => p.UserId, (d, p) => d.Id)
            .CountAsync(cancellationToken);

        var pendingFaces = await db.CountPendingFaceEnrollmentsAsync(scope, cancellationToken);

        var groupIds = scope.StudentGroupIds;
        var groups = groupIds.Count == 0
            ? []
            : (await db.StudentGroups.AsNoTracking()
                    .Where(g => groupIds.Contains(g.Id))
                    .Select(g => g.Name)
                    .ToListAsync(cancellationToken))
                .Order(StringComparer.Ordinal)
                .ToList();

        var periodName = groupIds.Count == 0 ? null : await CurrentPeriodNameAsync(groupIds, cancellationToken);

        return new TutorNavDto(
            new TutorNavCounts(students, applications, students, diaries, pendingFaces),
            new TutorNavContext(groups, periodName));
    }

    /// <summary>Har guruhning sukut davri (<see cref="PeriodPurpose.Default"/>: davom etayotgan → oxirgi tugagan → kelgusi);
    /// guruhlar turli davrlarda bo'lsa, shu nomzodlarga yana o'sha qoida qo'llanadi (davom etayotgani ustun), teng
    /// holatda — ko'proq guruhning davri, keyin kechroq boshlangani.</summary>
    private async Task<string?> CurrentPeriodNameAsync(IReadOnlyList<Guid> groupIds, CancellationToken cancellationToken)
    {
        var periods = await db.PracticePeriods.AsNoTracking()
            .Where(p => p.Groups.Any(g => groupIds.Contains(g.StudentGroupId)))
            .Select(p => new PeriodCandidate(
                p.Name,
                p.StartDate,
                p.EndDate,
                p.Status == PracticePeriodStatus.Closed,
                p.Groups.Where(g => groupIds.Contains(g.StudentGroupId)).Select(g => g.StudentGroupId).ToList()))
            .ToListAsync(cancellationToken);

        if (periods.Count == 0)
            return null;

        var today = clock.LocalToday();
        var ranked = groupIds
            .Select(groupId => PeriodSelection.Select(
                periods.Where(p => p.GroupIds.Contains(groupId)), today, PeriodPurpose.Default, p => p.Span))
            .OfType<PeriodCandidate>()
            .GroupBy(p => p)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key.StartDate)
            .Select(g => g.Key)
            .ToList();

        return PeriodSelection.Select(ranked, today, PeriodPurpose.Default, p => p.Span)?.Name;
    }

    private sealed class PeriodCandidate(string name, DateOnly startDate, DateOnly endDate, bool isClosed, List<Guid> groupIds)
    {
        public string Name { get; } = name;
        public DateOnly StartDate { get; } = startDate;
        public List<Guid> GroupIds { get; } = groupIds;
        public PeriodSpan Span { get; } = new(startDate, endDate, isClosed);
    }
}

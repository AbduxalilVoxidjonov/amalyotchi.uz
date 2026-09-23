using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Common.Practice;

/// <summary>Talaba profilidagi davr tanlagichi elementi.
/// <paramref name="Status"/> — <see cref="PracticePeriod.ResolveStatus"/> (planned / active / closed);
/// <paramref name="IsDefault"/> — so'rovda <c>periodId</c> berilmaganda tanlanadigan davr.</summary>
public sealed record StudentPeriodOption(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    PracticePeriodStatus Status,
    bool IsDefault);

/// <summary>Talabaning davrlari: guruhiga biriktirilgan barcha davrlar ∪ talabaning <c>PeriodId</c> li yozuvlari
/// (ariza, davomat, kundalik, baho, ruxsat) bor davrlar — guruh almashgan talaba eski davrini yo'qotmaydi.
/// <see cref="GroupPeriods"/> — faqat joriy guruhnikilar (sukut bo'yicha davr shulardan tanlanadi).</summary>
public sealed record StudentPeriodSet(IReadOnlyList<PracticePeriod> All, IReadOnlyList<PracticePeriod> GroupPeriods)
{
    public const string PeriodNotFoundMessage = "Amaliyot davri topilmadi.";

    /// <summary>Joriy guruh davrlaridan <paramref name="purpose"/> qoidasi bilan tanlangan davr. Faqat ko'rish maqsadida
    /// (<see cref="PeriodPurpose.Default"/>) guruhda mos davr bo'lmasa talabaning barcha davrlaridan (eski guruhi) olinadi —
    /// check-in/ariza esa faqat joriy guruh davrlariga bog'lanadi.</summary>
    public PracticePeriod? Default(DateOnly today, PeriodPurpose purpose = PeriodPurpose.Default)
        => PeriodSelection.Select(GroupPeriods, today, purpose)
           ?? (purpose == PeriodPurpose.Default ? PeriodSelection.Select(All, today, purpose) : null);

    /// <summary><paramref name="periodId"/> berilsa — talabaning davrlaridan biri bo'lishi shart (aks holda 404);
    /// berilmasa — <see cref="Default"/>.</summary>
    public PracticePeriod? Resolve(Guid? periodId, DateOnly today, PeriodPurpose purpose = PeriodPurpose.Default)
    {
        if (periodId is not { } id)
            return Default(today, purpose);

        return All.FirstOrDefault(p => p.Id == id) ?? throw new NotFoundException(PeriodNotFoundMessage);
    }

    /// <summary>Tanlagich ro'yxati — <c>startDate</c> kamayish tartibida.</summary>
    public IReadOnlyList<StudentPeriodOption> Options(DateOnly today, Guid? defaultPeriodId)
        => All
            .OrderByDescending(p => p.StartDate)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => ToOption(p, today, p.Id == defaultPeriodId))
            .ToList();

    public static StudentPeriodOption ToOption(PracticePeriod period, DateOnly today, bool isDefault)
        => new(period.Id, period.Name, period.StartDate, period.EndDate, period.EffectiveStatus(today), isDefault);
}

internal static class StudentPeriodQueries
{
    /// <summary>Talabaning davrlar to'plami (o'chirilgan davrlar global filtr bilan chiqib ketadi). Entity'lar kuzatilmaydi.</summary>
    public static async Task<StudentPeriodSet> LoadStudentPeriodsAsync(
        this IApplicationDbContext db, Guid studentUserId, Guid groupId, CancellationToken cancellationToken)
    {
        var recordPeriodIds = await db.DailyAttendances.Where(a => a.StudentUserId == studentUserId).Select(a => a.PeriodId)
            .Union(db.PracticeApplications.Where(a => a.StudentUserId == studentUserId).Select(a => a.PeriodId))
            .Union(db.DiaryEntries.Where(d => d.StudentUserId == studentUserId).Select(d => d.PeriodId))
            .Union(db.PracticeGrades.Where(g => g.StudentUserId == studentUserId).Select(g => g.PeriodId))
            .Union(db.LeaveRequests.Where(l => l.StudentUserId == studentUserId).Select(l => l.PeriodId))
            .ToListAsync(cancellationToken);

        var periods = await db.PracticePeriods
            .AsNoTracking()
            .Include(p => p.Groups)
            .Where(p => p.Groups.Any(g => g.StudentGroupId == groupId) || recordPeriodIds.Contains(p.Id))
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);

        var groupPeriods = periods.Where(p => p.IncludesGroup(groupId)).ToList();
        return new StudentPeriodSet(periods, groupPeriods);
    }
}

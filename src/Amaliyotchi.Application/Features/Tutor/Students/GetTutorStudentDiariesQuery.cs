using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Tutor.Diaries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Students;

/// <summary><c>GET /api/tutor/students/{id}/diaries?periodId=</c> — talabaning tanlangan davrdagi kundaliklari
/// (<c>periodId</c> berilmasa — sukut bo'yicha davr; talabaning umuman davri bo'lmasa — hammasi),
/// sana bo'yicha kamayish tartibida. Ko'lamdan tashqari talaba yoki begona <c>periodId</c> → 404.</summary>
public sealed record GetTutorStudentDiariesQuery(Guid StudentId, Guid? PeriodId = null) : IRequest<IReadOnlyList<TutorDiaryEntry>>;

internal sealed class GetTutorStudentDiariesQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock)
    : IRequestHandler<GetTutorStudentDiariesQuery, IReadOnlyList<TutorDiaryEntry>>
{
    public async Task<IReadOnlyList<TutorDiaryEntry>> Handle(
        GetTutorStudentDiariesQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);

        // Ko'lamdan tashqari (yoki umuman yo'q) talaba → NotFoundException (404).
        var profile = await db.GetScopedStudentAsync(scope, request.StudentId, cancellationToken);

        var periodSet = await db.LoadStudentPeriodsAsync(profile.UserId, profile.StudentGroupId, cancellationToken);
        var selected = periodSet.Resolve(request.PeriodId, clock.LocalToday());

        var query = db.DiaryEntries.AsNoTracking().InScope(scope).Where(d => d.StudentUserId == request.StudentId);
        if (selected is not null)
        {
            var periodId = selected.Id;
            query = query.Where(d => d.PeriodId == periodId);
        }

        var entries = await query.SelectTutorEntriesAsync(db, cancellationToken);

        return entries
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.SubmittedAt)
            .ToList();
    }
}

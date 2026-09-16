using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Features.Tutor.Diaries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Students;

/// <summary><c>GET /api/tutor/students/{id}/diaries</c> — talabaning barcha kundaliklari,
/// sana bo'yicha kamayish tartibida. Ko'lamdan tashqari talaba → 404.</summary>
public sealed record GetTutorStudentDiariesQuery(Guid StudentId) : IRequest<IReadOnlyList<TutorDiaryEntry>>;

internal sealed class GetTutorStudentDiariesQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver)
    : IRequestHandler<GetTutorStudentDiariesQuery, IReadOnlyList<TutorDiaryEntry>>
{
    public async Task<IReadOnlyList<TutorDiaryEntry>> Handle(
        GetTutorStudentDiariesQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);

        // Ko'lamdan tashqari (yoki umuman yo'q) talaba → NotFoundException (404).
        await db.GetScopedStudentAsync(scope, request.StudentId, cancellationToken);

        var entries = await db.DiaryEntries.AsNoTracking().InScope(scope)
            .Where(d => d.StudentUserId == request.StudentId)
            .SelectTutorEntriesAsync(db, cancellationToken);

        return entries
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.SubmittedAt)
            .ToList();
    }
}

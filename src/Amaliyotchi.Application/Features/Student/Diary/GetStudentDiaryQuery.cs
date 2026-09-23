using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.Diary;

/// <summary><c>GET /api/student/diary</c> — faqat o'z yozuvlari, yangisi birinchi (barcha davrlar bo'yicha;
/// har yozuvda <c>periodId</c>/<c>periodName</c>).</summary>
public sealed record GetStudentDiaryQuery : IRequest<IReadOnlyList<DiaryEntryDto>>;

internal sealed class GetStudentDiaryQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetStudentDiaryQuery, IReadOnlyList<DiaryEntryDto>>
{
    public async Task<IReadOnlyList<DiaryEntryDto>> Handle(GetStudentDiaryQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");

        var entries = await db.DiaryEntries
            .AsNoTracking()
            .Include(d => d.Attachments)
            .Where(d => d.StudentUserId == userId)
            .OrderByDescending(d => d.Date)
            .ThenByDescending(d => d.SubmittedAt)
            .ToListAsync(cancellationToken);

        var periodIds = entries.Select(e => e.PeriodId).Distinct().ToList();
        var periodNames = await db.PracticePeriods
            .AsNoTracking()
            .Where(p => periodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return entries.Select(e => DiaryMapping.ToDto(e, periodNames.GetValueOrDefault(e.PeriodId))).ToList();
    }
}

internal static class DiaryMapping
{
    public static DiaryEntryDto ToDto(DiaryEntry entry, string? periodName) => new(
        entry.Id,
        entry.Date,
        entry.SubmittedAt,
        entry.Status,
        entry.Text,
        entry.Learned,
        entry.Attachments
            .Select(a => new DiaryFileDto(a.StoredFileId, a.FileName, StudentQueries.FileUrl(a.StoredFileId)))
            .ToList(),
        entry.Score,
        entry.TutorComment,
        entry.PeriodId,
        periodName);
}

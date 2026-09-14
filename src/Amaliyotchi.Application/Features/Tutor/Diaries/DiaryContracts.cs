using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Diary;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Diaries;

public enum DiaryReviewAction
{
    Approve = 1,
    Score = 2,
    Rewrite = 3
}

public sealed record DiaryFile(string Name, string Url);

public sealed record TutorDiaryEntry(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string Group,
    DateOnly Date,
    DateTimeOffset SubmittedAt,
    DiaryStatus Status,
    string Text,
    string? Learned,
    IReadOnlyList<DiaryFile> Files,
    int? Score,
    string? Comment,
    DateTimeOffset? ReviewedAt);

internal static class DiaryQueries
{
    /// <summary>Talaba profili bilan birlashtirilgan proyeksiya; fayl havolalari xotirada quriladi.</summary>
    public static async Task<List<TutorDiaryEntry>> SelectTutorEntriesAsync(
        this IQueryable<DiaryEntry> entries, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var rows = await entries
            .Join(db.StudentProfiles, d => d.StudentUserId, p => p.UserId, (d, p) => new
            {
                d.Id,
                d.StudentUserId,
                StudentName = p.User.FullName,
                GroupName = p.Group.Name,
                d.Date,
                d.SubmittedAt,
                d.Status,
                d.Text,
                d.Learned,
                d.Score,
                d.TutorComment,
                d.ReviewedAt,
                Files = d.Attachments.Select(a => new { a.FileName, a.StoredFileId }).ToList()
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new TutorDiaryEntry(
                r.Id, r.StudentUserId, r.StudentName, r.GroupName, r.Date, r.SubmittedAt, r.Status, r.Text, r.Learned,
                r.Files.Select(f => new DiaryFile(f.FileName, FileUrls.For(f.StoredFileId))).ToList(),
                r.Score, r.TutorComment, r.ReviewedAt))
            .ToList();
    }
}

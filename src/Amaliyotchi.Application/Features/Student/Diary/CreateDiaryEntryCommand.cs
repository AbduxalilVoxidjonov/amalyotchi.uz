using Amaliyotchi.Application.Common.Exceptions;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Files;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.Diary;

/// <summary><c>POST /api/student/diary</c> (multipart). Bugungi hisobot: yangi → 201; bugungisi "qayta yozish"
/// holatida bo'lsa — <see cref="DiaryEntry.Resubmit"/> (yangi fayllar qo'shiladi); aks holda 409.
/// Sozlama <c>diaryPdfRequired</c> yoqilgan bo'lsa kamida bitta PDF shart (qayta yozishda avvalgi PDF ham hisob) → 400 <c>errors.Files</c>.
/// Fayllar avval saqlovchiga yoziladi, keyin bitta <c>SaveChanges</c>; baza xatosida fayllar tozalanadi.</summary>
public sealed record CreateDiaryEntryCommand(string Text, string? Learned, IReadOnlyList<UploadedFile> Files)
    : IRequest<DiaryEntryDto>;

internal sealed class CreateDiaryEntryCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IClock clock,
    IFileStorage storage)
    : IRequestHandler<CreateDiaryEntryCommand, DiaryEntryDto>
{
    public async Task<DiaryEntryDto> Handle(CreateDiaryEntryCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var now = clock.UtcNow;
        var today = clock.LocalToday();

        var practice = await db.LoadStudentPracticeAsync(userId, today, cancellationToken);
        var period = practice.Period ?? throw new DomainException("Faol amaliyot davri yo'q — hisobot yozib bo'lmaydi.");
        if (!period.Contains(today))
            throw new DomainException("Amaliyot davri bugunni o'z ichiga olmaydi.");

        var existing = await db.DiaryEntries
            .Include(d => d.Attachments)
            .FirstOrDefaultAsync(d => d.StudentUserId == userId && d.Date == today, cancellationToken);

        DiaryEntry entry;
        if (existing is null)
        {
            entry = DiaryEntry.Create(userId, period.Id, today, request.Text, request.Learned, now, practice.Settings.MinReportLength);
            db.DiaryEntries.Add(entry);
        }
        else if (existing.Status == DiaryStatus.Rewrite)
        {
            existing.Resubmit(request.Text, request.Learned, now, practice.Settings.MinReportLength);
            entry = existing;
        }
        else
        {
            throw new ConflictException("Bugungi hisobot allaqachon yuborilgan.");
        }

        if (practice.Settings.DiaryPdfRequired && !await HasPdfAsync(entry, request.Files, cancellationToken))
            throw new ValidationException(
                new Dictionary<string, string[]> { ["Files"] = [PdfRequiredMessage] }, PdfRequiredMessage);

        if (entry.Attachments.Count + request.Files.Count > DiaryEntry.MaxAttachments)
            throw new DomainException($"Bitta hisobotga ko'pi bilan {DiaryEntry.MaxAttachments} ta fayl biriktiriladi.");

        var savedKeys = new List<string>();
        try
        {
            foreach (var upload in request.Files)
            {
                await using var content = upload.OpenRead();
                var key = await storage.SaveAsync(content, upload.FileName, upload.ContentType, cancellationToken);
                savedKeys.Add(key);

                var stored = StoredFile.Create(
                    StoredFileKind.DiaryAttachment, upload.FileName, upload.ContentType, upload.Length, key, now, userId);
                db.StoredFiles.Add(stored);
                entry.AddAttachment(stored.Id, upload.FileName, upload.Length);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (var key in savedKeys)
                await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        return DiaryMapping.ToDto(entry);
    }

    public const string PdfRequiredMessage = "Hisobotga PDF fayl biriktirilishi shart.";

    /// <summary>Yangi fayllar orasida yoki (qayta yozishda) yozuvda qolgan biriktirmalar orasida PDF bormi.</summary>
    private async Task<bool> HasPdfAsync(DiaryEntry entry, IReadOnlyList<UploadedFile> files, CancellationToken cancellationToken)
    {
        if (files.Any(f => CreateDiaryEntryCommandValidator.IsPdf(f.ContentType, f.FileName)))
            return true;
        if (entry.Attachments.Count == 0)
            return false;

        var ids = entry.Attachments.Select(a => a.StoredFileId).ToList();
        var stored = await db.StoredFiles
            .AsNoTracking()
            .Where(f => ids.Contains(f.Id))
            .Select(f => new { f.ContentType, f.FileName })
            .ToListAsync(cancellationToken);
        return stored.Any(f => CreateDiaryEntryCommandValidator.IsPdf(f.ContentType, f.FileName))
            || entry.Attachments.Any(a => CreateDiaryEntryCommandValidator.IsPdf(null, a.FileName));
    }
}

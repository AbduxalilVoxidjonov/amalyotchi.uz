using Amaliyotchi.Application.Common.Exceptions;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.Diary;

/// <summary><c>POST /api/student/diary</c> (multipart). Hisobot kuni — <see cref="Date"/> (berilmasa bugun, Toshkent).
/// Qoida — <see cref="DiaryWritePolicy"/>: <c>date</c> berilmagan eski yo'lda (bugun) davr xatosi 400 (<c>detail</c>);
/// <c>date</c> berilganda kelajak / o'tgan kun (sozlama <c>diaryAllowPastDays</c> o'chiq, yozuv <c>rewrite</c> emas) /
/// davr tashqarisi / ish kuni emas → 400 <c>errors.Date</c>. Shu kunga yozuv yo'q → 201 (yangi);
/// "qayta yozish" holatida → <see cref="DiaryEntry.Resubmit"/> (yangi fayllar qo'shiladi, holat <c>submitted</c>); aks holda 409.
/// <see cref="Files"/> ichida kamida bitta PDF bo'lsa <see cref="Text"/> ixtiyoriy (bo'sh satr saqlanadi, minimal uzunlik
/// tekshirilmaydi); PDF bo'lmasa — matn majburiy va <c>minReportLength</c> dan qisqa bo'lmasin.
/// Sozlama <c>diaryPdfRequired</c> yoqilgan bo'lsa kamida bitta PDF shart (qayta yozishda avvalgi PDF ham hisob) → 400 <c>errors.Files</c>.
/// Fayllar avval saqlovchiga yoziladi, keyin bitta <c>SaveChanges</c>; baza xatosida fayllar tozalanadi.</summary>
public sealed record CreateDiaryEntryCommand(
    string Text, string? Learned, IReadOnlyList<UploadedFile> Files, DateOnly? Date = null)
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

        // Yagona qoida — DiaryWritePolicy (GET today → canWriteDiary, GET period-days → canSubmitDiary ham shundan);
        // yakunlangan davrda yozuvni qayta yozish ham taqiqlanadi.
        var practice = await db.LoadStudentPracticeAsync(userId, today, PeriodPurpose.Ongoing, cancellationToken);
        var date = request.Date ?? today;

        var existing = await db.DiaryEntries
            .Include(d => d.Attachments)
            .FirstOrDefaultAsync(d => d.StudentUserId == userId && d.Date == date, cancellationToken);

        PracticePeriod period;
        if (request.Date is null)
        {
            // Eski yo'l (TWA): bugun, ish kuni sharti yo'q, davr xatosi — DomainException (400, faqat detail).
            period = DiaryWritePolicy.RequireOngoingPeriod(practice);
            if (existing is not null && existing.Status != DiaryStatus.Rewrite)
                throw new ConflictException(DiaryWritePolicy.AlreadySubmittedMessage);
        }
        else
        {
            var decision = DiaryWritePolicy.EvaluateDay(practice, date, existing?.Status);
            if (decision.Block == DiaryDayBlock.AlreadySubmitted)
                throw new ConflictException(decision.Reason!);
            if (!decision.CanWrite)
                throw new ValidationException(
                    new Dictionary<string, string[]> { [nameof(CreateDiaryEntryCommand.Date)] = [decision.Reason!] },
                    decision.Reason);
            period = decision.Period!;
        }

        // Yangi fayllar orasida PDF bo'lsa matn ixtiyoriy — minimal uzunlik qo'llanmaydi.
        var minLength = HasPdf(request.Files) ? 0 : practice.Settings.MinReportLength;
        var text = request.Text ?? string.Empty;

        DiaryEntry entry;
        if (existing is null)
        {
            entry = DiaryEntry.Create(userId, period.Id, date, text, request.Learned, now, minLength);
            db.DiaryEntries.Add(entry);
        }
        else
        {
            existing.Resubmit(text, request.Learned, now, minLength);
            entry = existing;
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

        return DiaryMapping.ToDto(entry, period.Name);
    }

    public const string PdfRequiredMessage = "Hisobotga PDF fayl biriktirilishi shart.";

    /// <summary>Yangi fayllar orasida PDF bormi (matn ixtiyoriyligi shu bilan aniqlanadi).</summary>
    internal static bool HasPdf(IReadOnlyList<UploadedFile>? files)
        => files is not null && files.Any(f => CreateDiaryEntryCommandValidator.IsPdf(f.ContentType, f.FileName));

    /// <summary>Yangi fayllar orasida yoki (qayta yozishda) yozuvda qolgan biriktirmalar orasida PDF bormi.</summary>
    private async Task<bool> HasPdfAsync(DiaryEntry entry, IReadOnlyList<UploadedFile> files, CancellationToken cancellationToken)
    {
        if (HasPdf(files))
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

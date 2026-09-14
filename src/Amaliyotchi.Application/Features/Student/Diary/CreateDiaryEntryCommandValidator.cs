using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Diary;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Student.Diary;

/// <summary>Matn uzunligi (minimal — sozlamadan), fayllar soni/hajmi/turi. Xatolar <c>errors.Text</c>, <c>errors.Files</c>.</summary>
public sealed class CreateDiaryEntryCommandValidator : AbstractValidator<CreateDiaryEntryCommand>
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;

    public static readonly IReadOnlySet<string> AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/heic", "image/heif", "application/pdf"
    };

    public CreateDiaryEntryCommandValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Hisobot matnini kiriting.")
            .MaximumLength(DiaryEntry.TextMaxLength).WithMessage($"Hisobot matni {DiaryEntry.TextMaxLength} belgidan oshmasligi kerak.")
            .MustAsync(async (text, ct) =>
            {
                var settings = await db.LoadStudentSettingsAsync(ct);
                return (text?.Trim().Length ?? 0) >= settings.MinReportLength;
            })
            .WithMessage("Hisobot matni juda qisqa — minimal uzunlik sozlamada belgilangan.")
            .When(x => !string.IsNullOrWhiteSpace(x.Text), ApplyConditionTo.CurrentValidator);

        RuleFor(x => x.Learned)
            .MaximumLength(DiaryEntry.TextMaxLength).WithMessage($"\"Nimani o'rgandim\" {DiaryEntry.TextMaxLength} belgidan oshmasligi kerak.");

        RuleFor(x => x.Files)
            .NotNull()
            .Must(f => f.Count <= DiaryEntry.MaxAttachments)
            .WithMessage($"Ko'pi bilan {DiaryEntry.MaxAttachments} ta fayl biriktirish mumkin.")
            .Must(f => f.All(x => x.Length > 0 && x.Length <= MaxFileSizeBytes))
            .WithMessage("Har bir fayl 5 MB dan oshmasligi va bo'sh bo'lmasligi kerak.")
            .Must(f => f.All(x => AllowedContentTypes.Contains(x.ContentType)))
            .WithMessage("Faqat rasm (JPEG, PNG, WebP, HEIC) yoki PDF fayllar qabul qilinadi.")
            .Must(f => f.All(x => !string.IsNullOrWhiteSpace(x.FileName) && x.FileName.Trim().Length <= DiaryAttachment.FileNameMaxLength))
            .WithMessage("Fayl nomi bo'sh yoki juda uzun.");
    }
}

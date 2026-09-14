using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Diary;

/// <summary>Kundalik yozuviga biriktirilgan fayl (rasm/hujjat). Fayl o'zi <c>StoredFile</c> da.</summary>
public sealed class DiaryAttachment : BaseEntity
{
    public const int FileNameMaxLength = 255;

    private DiaryAttachment() { }

    public Guid DiaryEntryId { get; private set; }
    public Guid StoredFileId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }

    internal static DiaryAttachment Create(Guid diaryEntryId, Guid storedFileId, string fileName, long sizeBytes)
    {
        if (storedFileId == Guid.Empty)
            throw new DomainException("Fayl ko'rsatilmagan.");
        var trimmed = fileName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("Fayl nomi bo'sh bo'lishi mumkin emas.");
        if (trimmed.Length > FileNameMaxLength)
            throw new DomainException($"Fayl nomi {FileNameMaxLength} belgidan oshmasligi kerak.");
        if (sizeBytes < 0)
            throw new DomainException("Fayl hajmi manfiy bo'lishi mumkin emas.");

        return new DiaryAttachment
        {
            DiaryEntryId = diaryEntryId,
            StoredFileId = storedFileId,
            FileName = trimmed,
            SizeBytes = sizeBytes
        };
    }
}

using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Files;

/// <summary>Saqlangan fayl metama'lumoti. Bayt'lar <c>IFileStorage</c> da (lokal disk / MinIO),
/// bu yerda faqat yo'l. O'zgarmas; audit'dan istisno.</summary>
public sealed class StoredFile : BaseEntity, IAuditExempt
{
    public const int FileNameMaxLength = 255;
    public const int ContentTypeMaxLength = 100;
    public const int StoragePathMaxLength = 500;

    private StoredFile() { }

    public StoredFileKind Kind { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }

    /// <summary>Saqlash tizimidagi nisbiy yo'l/kalit.</summary>
    public string StoragePath { get; private set; } = string.Empty;

    /// <summary>PDF sahifalar soni (aniqlangan bo'lsa).</summary>
    public int? Pages { get; private set; }
    public Guid? UploadedByUserId { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }

    public static StoredFile Create(
        StoredFileKind kind,
        string fileName,
        string contentType,
        long sizeBytes,
        string storagePath,
        DateTimeOffset uploadedAt,
        Guid? uploadedByUserId = null,
        int? pages = null)
    {
        var name = fileName?.Trim();
        if (string.IsNullOrEmpty(name))
            throw new DomainException("Fayl nomi bo'sh bo'lishi mumkin emas.");
        if (name.Length > FileNameMaxLength)
            throw new DomainException($"Fayl nomi {FileNameMaxLength} belgidan oshmasligi kerak.");
        var type = contentType?.Trim();
        if (string.IsNullOrEmpty(type))
            throw new DomainException("Fayl turi (content-type) bo'sh bo'lishi mumkin emas.");
        if (type.Length > ContentTypeMaxLength)
            throw new DomainException($"Fayl turi {ContentTypeMaxLength} belgidan oshmasligi kerak.");
        if (sizeBytes <= 0)
            throw new DomainException("Bo'sh fayl saqlanmaydi.");
        var path = storagePath?.Trim();
        if (string.IsNullOrEmpty(path))
            throw new DomainException("Saqlash yo'li bo'sh bo'lishi mumkin emas.");
        if (path.Length > StoragePathMaxLength)
            throw new DomainException($"Saqlash yo'li {StoragePathMaxLength} belgidan oshmasligi kerak.");
        if (pages is < 0)
            throw new DomainException("Sahifalar soni manfiy bo'lishi mumkin emas.");

        return new StoredFile
        {
            Kind = kind,
            FileName = name,
            ContentType = type,
            SizeBytes = sizeBytes,
            StoragePath = path,
            Pages = pages,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = uploadedAt
        };
    }

    public void SetPages(int pages)
    {
        if (pages < 0)
            throw new DomainException("Sahifalar soni manfiy bo'lishi mumkin emas.");
        Pages = pages;
    }
}

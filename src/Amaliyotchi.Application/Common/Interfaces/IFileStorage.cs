namespace Amaliyotchi.Application.Common.Interfaces;

/// <summary>Saqlangan faylning oqimi va turi. Oqimni chaqiruvchi yopadi.</summary>
public sealed record StoredFileContent(Stream Content, string ContentType, long Length);

/// <summary>Fayl saqlash — lokal disk (hozir) yoki MinIO (keyin). Interfeys o'zgarmaydi.
/// Kalit (<c>key</c>) — saqlovchi ichidagi nisbiy yo'l, bazada <c>StoredFile.StoragePath</c> sifatida saqlanadi.</summary>
public interface IFileStorage
{
    /// <summary>Faylni saqlab, kalitini qaytaradi. Kalitda asl nom yo'q (xavfsizlik) — faqat kengaytma.</summary>
    Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Topilmasa <c>null</c>.</summary>
    Task<StoredFileContent?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Yo'q faylni o'chirish xato emas.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}

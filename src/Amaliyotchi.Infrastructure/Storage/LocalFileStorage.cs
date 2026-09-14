using Amaliyotchi.Application.Common.Interfaces;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace Amaliyotchi.Infrastructure.Storage;

/// <summary>Fayllar lokal diskda: <c>{RootPath}/yyyy/MM/{uuid}{.ext}</c>. Kalit — shu nisbiy yo'l
/// (<c>/</c> bilan). Asl fayl nomi kalitga kirmaydi — u bazada (<c>StoredFile.FileName</c>) saqlanadi.
/// MinIO ga o'tishda faqat shu klass almashadi.</summary>
public sealed class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private const string DefaultContentType = "application/octet-stream";
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task<string> SaveAsync(
        Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var now = DateTime.UtcNow;
        var extension = SafeExtension(fileName);
        var key = $"{now:yyyy}/{now:MM}/{Guid.CreateVersion7():N}{extension}";

        var fullPath = Resolve(key) ?? throw new InvalidOperationException("Fayl kaliti yaratib bo'lmadi.");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var target = new FileStream(
            fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        await content.CopyToAsync(target, cancellationToken);

        return key;
    }

    public Task<StoredFileContent?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(key);
        if (fullPath is null || !File.Exists(fullPath))
            return Task.FromResult<StoredFileContent?>(null);

        var stream = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);

        var contentType = ContentTypes.TryGetContentType(fullPath, out var known) ? known : DefaultContentType;
        return Task.FromResult<StoredFileContent?>(new StoredFileContent(stream, contentType, stream.Length));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(key);
        if (fullPath is not null && File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    /// <summary>Kalitni ildiz ichidagi to'liq yo'lga aylantiradi; ildizdan chiqib ketadigan
    /// (<c>..</c>, absolyut yo'l) yoki shubhali belgili kalit uchun <c>null</c>.</summary>
    private string? Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 260)
            return null;

        foreach (var c in key)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '/' or '.' or '-' or '_'))
                return null;
        }

        if (key.Contains("..", StringComparison.Ordinal) || key.StartsWith('/'))
            return null;

        var fullPath = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal) ? fullPath : null;
    }

    private static string SafeExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension) || extension.Length > 10)
            return string.Empty;

        return extension.All(c => char.IsAsciiLetterOrDigit(c) || c == '.')
            ? extension.ToLowerInvariant()
            : string.Empty;
    }
}

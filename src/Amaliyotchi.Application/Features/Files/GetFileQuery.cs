using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Files;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Files;

/// <summary>Yuklab olish uchun fayl: metama'lumot (asl nom, tur) + oqim. Oqimni chaqiruvchi yopadi.</summary>
public sealed record FileDownloadDto(StoredFile File, Stream Content, long Length);

/// <summary><c>GET /api/files/{id}</c>. Ko'lam: shablon — hamma; admin — hammasi; talaba — o'z fayllari
/// (o'zi yuklagan yoki o'z arizasi/kundaligi/ruxsatiga biriktirilgan); tyutor — ko'lamdagi talabalar fayllari.
/// Ko'lamdan tashqari yoki yo'q fayl — 404 (mavjudligi oshkor qilinmaydi).</summary>
public sealed record GetFileQuery(Guid FileId) : IRequest<FileDownloadDto>;

internal sealed class GetFileQueryHandler(
    IApplicationDbContext db,
    IFileStorage storage,
    IScopeResolver scopeResolver)
    : IRequestHandler<GetFileQuery, FileDownloadDto>
{
    public async Task<FileDownloadDto> Handle(GetFileQuery request, CancellationToken cancellationToken)
    {
        var file = await db.StoredFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.FileId, cancellationToken)
            ?? throw NotFound(request.FileId);

        if (!await IsAllowedAsync(file, cancellationToken))
            throw NotFound(request.FileId);

        var content = await storage.OpenReadAsync(file.StoragePath, cancellationToken)
            ?? throw NotFound(request.FileId);

        return new FileDownloadDto(file, content.Content, content.Length);
    }

    private async Task<bool> IsAllowedAsync(StoredFile file, CancellationToken cancellationToken)
    {
        if (file.Kind == StoredFileKind.Template)
            return true;

        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        if (scope.IsUnrestricted)
            return true;
        if (scope.Kind == ScopeKind.None)
            return false;

        var ownerId = await ResolveOwnerAsync(file, cancellationToken);
        return ownerId is { } owner && scope.Includes(owner);
    }

    /// <summary>Fayl qaysi talabaga tegishli: ariza shartnomasi → kundalik ilovasi → ruxsat hujjati → yuklovchi.</summary>
    private async Task<Guid?> ResolveOwnerAsync(StoredFile file, CancellationToken cancellationToken)
    {
        var fileId = file.Id;

        var fromApplication = await db.PracticeApplications
            .Where(a => a.ContractFileId == fileId)
            .Select(a => (Guid?)a.StudentUserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (fromApplication is not null)
            return fromApplication;

        var fromDiary = await db.DiaryAttachments
            .Where(a => a.StoredFileId == fileId)
            .Join(db.DiaryEntries, a => a.DiaryEntryId, d => d.Id, (_, d) => (Guid?)d.StudentUserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (fromDiary is not null)
            return fromDiary;

        var fromLeave = await db.LeaveRequests
            .Where(l => l.DocumentFileId == fileId)
            .Select(l => (Guid?)l.StudentUserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (fromLeave is not null)
            return fromLeave;

        return file.UploadedByUserId;
    }

    private static NotFoundException NotFound(Guid id) => new("Fayl", id);
}

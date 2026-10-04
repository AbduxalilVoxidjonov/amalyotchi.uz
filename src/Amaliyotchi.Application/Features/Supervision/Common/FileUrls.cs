namespace Amaliyotchi.Application.Features.Supervision.Common;

/// <summary>Saqlangan fayl havolasi — <c>FilesController</c> marshruti bilan bir xil.</summary>
public static class FileUrls
{
    public static string For(Guid storedFileId) => $"/api/files/{storedFileId}";
}

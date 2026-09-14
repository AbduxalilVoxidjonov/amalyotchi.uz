namespace Amaliyotchi.Application.Features.Tutor.Common;

/// <summary>Saqlangan fayl havolasi — <c>FilesController</c> marshruti bilan bir xil.</summary>
public static class FileUrls
{
    public static string For(Guid storedFileId) => $"/api/files/{storedFileId}";
}

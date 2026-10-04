using Amaliyotchi.Application.Common.Exceptions;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Student.Common;

namespace Amaliyotchi.Application.Features.Faces;

internal static class FaceEngineExtensions
{
    public const string UnavailableMessage =
        "Yuzni tasdiqlash xizmati vaqtincha ishlamayapti. Birozdan keyin qayta urinib ko'ring.";

    /// <summary>Yuklangan rasmni tahlil qiladi. Dvigatel tayyor emas (modellar yo'q) → 503 — hech qachon jimgina o'tkazilmaydi.</summary>
    public static async Task<FaceAnalysis> AnalyzeOrThrowAsync(
        this IFaceEngine engine, UploadedFile photo, CancellationToken cancellationToken)
    {
        if (!engine.IsReady)
            throw new ServiceUnavailableException(UnavailableMessage);

        await using var content = photo.OpenRead();
        return await engine.AnalyzeAsync(content, cancellationToken);
    }
}

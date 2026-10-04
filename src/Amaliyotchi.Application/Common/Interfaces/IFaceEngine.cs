namespace Amaliyotchi.Application.Common.Interfaces;

/// <summary>Bitta rasmning yuz tahlili.</summary>
/// <param name="Readable">Rasmni o'qib (decode qilib) bo'ldimi. <c>false</c> — format qo'llab-quvvatlanmaydi yoki fayl buzuq.</param>
/// <param name="FaceCount">Topilgan yuzlar soni (detektor ishonch chegarasidan o'tganlari).</param>
/// <param name="Embedding">ENG KATTA yuzning embedding'i (SFace: 128 son); yuz topilmasa — null.</param>
public sealed record FaceAnalysis(bool Readable, int FaceCount, float[]? Embedding)
{
    public static FaceAnalysis Unreadable { get; } = new(false, 0, null);
}

/// <summary>Yuzni aniqlash (detect) + embedding (embed) dvigateli — jarayon ichida, bulutsiz. Amalga oshirish
/// Infrastructure'da (YuNet + SFace ONNX, <c>Microsoft.ML.OnnxRuntime</c>); testlarda deterministik soxta dvigatel.
/// Singleton, thread-safe.</summary>
public interface IFaceEngine
{
    /// <summary>Modellar yuklanganmi. <c>false</c> bo'lsa <see cref="AnalyzeAsync"/> chaqirilmaydi —
    /// yuzni tasdiqlash yoqilgan bo'lsa so'rov 503 bilan tugaydi (jimgina o'tkazib yuborilmaydi).</summary>
    bool IsReady { get; }

    /// <summary>Tayyor bo'lmasa — sababi (log uchun; foydalanuvchiga ko'rsatilmaydi).</summary>
    string? UnavailableReason { get; }

    /// <summary>Rasmdagi yuzlarni topadi va eng katta yuz embedding'ini hisoblaydi (EXIF orientatsiyasi hisobga olinadi).</summary>
    Task<FaceAnalysis> AnalyzeAsync(Stream image, CancellationToken cancellationToken = default);
}

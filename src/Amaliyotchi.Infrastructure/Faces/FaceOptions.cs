namespace Amaliyotchi.Infrastructure.Faces;

/// <summary><c>Face</c> bo'limi. Modellar repoda yo'q — <c>deploy/scripts/fetch-face-models.sh</c> yuklaydi
/// (SHA256 tekshiruvi bilan): Docker image'da <c>/app/models/face</c>, lokal ishlab chiqishda <c>.models/face</c>.</summary>
public sealed class FaceOptions
{
    public const string SectionName = "Face";

    /// <summary>Modellar papkasi. Nisbiy yo'l avval ilova papkasiga (<c>AppContext.BaseDirectory</c>), keyin joriy
    /// papkaga nisbatan qidiriladi.</summary>
    public string ModelsPath { get; set; } = "models/face";

    /// <summary>OpenCV Zoo YuNet (MIT) — yuz detektori, kirish 640×640.</summary>
    public string DetectorModel { get; set; } = "face_detection_yunet_2023mar.onnx";

    /// <summary>OpenCV Zoo SFace (Apache-2.0) — 112×112 hizalangan yuzdan 128 o'lchamli embedding.</summary>
    public string RecognizerModel { get; set; } = "face_recognition_sface_2021dec.onnx";

    /// <summary>Detektor ishonch chegarasi (OpenCV Zoo demo standarti — 0.9).</summary>
    public float DetectionThreshold { get; set; } = 0.9f;

    /// <summary>NMS IoU chegarasi (OpenCV Zoo demo standarti — 0.3).</summary>
    public float NmsThreshold { get; set; } = 0.3f;

    /// <summary>Bitta inference uchun CPU oqimlari (umumiy serverda hamma yadrolarni egallamaslik uchun).</summary>
    public int IntraOpThreads { get; set; } = 2;

    /// <summary>Bir vaqtda tahlil qilinadigan rasmlar soni (xotira/CPU cho'qqisini cheklaydi).</summary>
    public int MaxConcurrency { get; set; } = 2;
}

namespace Amaliyotchi.Infrastructure.Faces;

/// <summary>Topilgan yuz: to'rtburchak (x, y, w, h) va 5 nuqta (o'ng ko'z, chap ko'z, burun, og'izning o'ng va chap
/// burchagi) — rasm koordinatalarida.</summary>
public sealed record DetectedFace(float X, float Y, float Width, float Height, float Score, float[] Landmarks)
{
    public float Area => Width * Height;

    public DetectedFace Scale(float factor)
        => new(X * factor, Y * factor, Width * factor, Height * factor, Score, Landmarks.Select(v => v * factor).ToArray());
}

/// <summary>YuNet (2023mar) chiqishlarini dekodlash — OpenCV <c>FaceDetectorYN</c> (face_detect.cpp) bilan bir xil:
/// har stride (8/16/32) katagi uchun <c>score = sqrt(clamp(cls)·clamp(obj))</c>, markaz = (katak + offset)·stride,
/// o'lcham = exp(·)·stride, nuqtalar = (katak + offset)·stride; keyin NMS. Sof funksiya (unit-test qilinadi).</summary>
public static class YuNetDecoder
{
    public static readonly int[] Strides = [8, 16, 32];

    /// <param name="cls">stride bo'yicha: [rows·cols] ishonch.</param>
    /// <param name="obj">stride bo'yicha: [rows·cols] obyektlik.</param>
    /// <param name="bbox">stride bo'yicha: [rows·cols·4].</param>
    /// <param name="kps">stride bo'yicha: [rows·cols·10].</param>
    public static List<DetectedFace> Decode(
        int inputWidth, int inputHeight,
        IReadOnlyList<float[]> cls, IReadOnlyList<float[]> obj, IReadOnlyList<float[]> bbox, IReadOnlyList<float[]> kps,
        float scoreThreshold, float nmsThreshold)
    {
        var candidates = new List<DetectedFace>();
        for (var s = 0; s < Strides.Length; s++)
        {
            var stride = Strides[s];
            var cols = inputWidth / stride;
            var rows = inputHeight / stride;
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var idx = (r * cols) + c;
                    var clsScore = Math.Clamp(cls[s][idx], 0f, 1f);
                    var objScore = Math.Clamp(obj[s][idx], 0f, 1f);
                    var score = MathF.Sqrt(clsScore * objScore);
                    if (score < scoreThreshold)
                        continue;

                    var b = bbox[s];
                    var cx = (c + b[(idx * 4) + 0]) * stride;
                    var cy = (r + b[(idx * 4) + 1]) * stride;
                    var w = MathF.Exp(b[(idx * 4) + 2]) * stride;
                    var h = MathF.Exp(b[(idx * 4) + 3]) * stride;

                    var landmarks = new float[10];
                    var k = kps[s];
                    for (var n = 0; n < 5; n++)
                    {
                        landmarks[2 * n] = (k[(idx * 10) + (2 * n)] + c) * stride;
                        landmarks[(2 * n) + 1] = (k[(idx * 10) + (2 * n) + 1] + r) * stride;
                    }

                    candidates.Add(new DetectedFace(cx - (w / 2f), cy - (h / 2f), w, h, score, landmarks));
                }
            }
        }

        return Nms(candidates, nmsThreshold);
    }

    /// <summary>Ochko'z NMS: ishonch bo'yicha kamayish tartibida, IoU &gt; chegara bo'lganlari tashlanadi.</summary>
    public static List<DetectedFace> Nms(List<DetectedFace> faces, float iouThreshold)
    {
        var ordered = faces.OrderByDescending(f => f.Score).ToList();
        var kept = new List<DetectedFace>();
        foreach (var face in ordered)
        {
            if (kept.All(k => IoU(k, face) <= iouThreshold))
                kept.Add(face);
        }

        return kept;
    }

    public static float IoU(DetectedFace a, DetectedFace b)
    {
        var x1 = Math.Max(a.X, b.X);
        var y1 = Math.Max(a.Y, b.Y);
        var x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        var y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);
        var inter = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var union = a.Area + b.Area - inter;
        return union <= 0 ? 0 : inter / union;
    }
}

namespace Amaliyotchi.Domain.Faces;

/// <summary>Ikki yuz embedding'ining o'xshashligi. Ball — kosinus o'xshashlik foizda: <c>round(max(0, cos) × 100)</c>,
/// 0–100. SFace modeli uchun tavsiya etilgan kosinus chegarasi ≈ 0.363 → standart chegara 36%
/// (sozlama <c>faceMatchThreshold</c>). Ball chegaraga TENG bo'lsa — mos (talaba foydasiga).</summary>
public static class FaceMatch
{
    public static double Cosine(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Count == 0 || a.Count != b.Count)
            throw new ArgumentException("Embedding o'lchamlari mos emas.");

        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Count; i++)
        {
            dot += (double)a[i] * b[i];
            na += (double)a[i] * a[i];
            nb += (double)b[i] * b[i];
        }

        if (na <= 0 || nb <= 0 || double.IsNaN(dot))
            return 0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    /// <summary>0–100 butun foiz: manfiy kosinus → 0.</summary>
    public static int Score(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        var cos = Cosine(a, b);
        return (int)Math.Round(Math.Clamp(cos, 0, 1) * 100, MidpointRounding.AwayFromZero);
    }

    public static bool IsMatch(int score, int thresholdPercent) => score >= thresholdPercent;
}

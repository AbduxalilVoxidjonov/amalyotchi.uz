namespace Amaliyotchi.Infrastructure.Faces;

/// <summary>5 nuqta bo'yicha yuzni 112×112 ga hizalash — OpenCV <c>FaceRecognizerSF.alignCrop</c> bilan bir xil shablon
/// (ArcFace standarti) va o'xshashlik o'zgartirishi (aylantirish + masshtab + siljish, eng kichik kvadratlar / Umeyama).</summary>
public static class FaceAligner
{
    public const int Size = 112;

    /// <summary>Maqsad nuqtalar (112×112): o'ng ko'z, chap ko'z, burun, og'iz o'ng va chap burchagi.</summary>
    public static readonly float[] Template =
    [
        38.2946f, 51.6963f,
        73.5318f, 51.5014f,
        56.0252f, 71.7366f,
        41.5493f, 92.3655f,
        70.7299f, 92.2041f
    ];

    /// <summary>src → dst o'xshashlik o'zgartirishi: x' = a·x − b·y + tx, y' = b·x + a·y + ty.</summary>
    public static (double A, double B, double Tx, double Ty) EstimateSimilarity(IReadOnlyList<float> src, IReadOnlyList<float> dst)
    {
        var n = src.Count / 2;
        double sx = 0, sy = 0, dx = 0, dy = 0;
        for (var i = 0; i < n; i++)
        {
            sx += src[2 * i];
            sy += src[(2 * i) + 1];
            dx += dst[2 * i];
            dy += dst[(2 * i) + 1];
        }

        sx /= n;
        sy /= n;
        dx /= n;
        dy /= n;

        double num1 = 0, num2 = 0, den = 0;
        for (var i = 0; i < n; i++)
        {
            var px = src[2 * i] - sx;
            var py = src[(2 * i) + 1] - sy;
            var qx = dst[2 * i] - dx;
            var qy = dst[(2 * i) + 1] - dy;
            num1 += (px * qx) + (py * qy);
            num2 += (px * qy) - (py * qx);
            den += (px * px) + (py * py);
        }

        if (den <= 0)
            throw new ArgumentException("Yuz nuqtalari buzilgan.");

        var a = num1 / den;
        var b = num2 / den;
        var tx = dx - ((a * sx) - (b * sy));
        var ty = dy - ((b * sx) + (a * sy));
        return (a, b, tx, ty);
    }

    /// <summary>SFace kirish tensori: 1×3×112×112, RGB, 0..255 (OpenCV <c>blobFromImage(aligned, 1, swapRB=true)</c>).</summary>
    public static float[] AlignToTensor(RgbImage image, IReadOnlyList<float> landmarks)
    {
        var (a, b, tx, ty) = EstimateSimilarity(landmarks, Template);
        // Teskari o'zgartirish: dst → src.
        var det = (a * a) + (b * b);
        var ia = a / det;
        var ib = b / det;

        const int plane = Size * Size;
        var tensor = new float[3 * plane];
        Span<float> rgb = stackalloc float[3];
        for (var v = 0; v < Size; v++)
        {
            for (var u = 0; u < Size; u++)
            {
                var px = u - tx;
                var py = v - ty;
                var x = (float)((ia * px) + (ib * py));
                var y = (float)((-ib * px) + (ia * py));
                image.Sample(x, y, rgb);
                var o = (v * Size) + u;
                tensor[o] = rgb[0];
                tensor[plane + o] = rgb[1];
                tensor[(2 * plane) + o] = rgb[2];
            }
        }

        return tensor;
    }
}

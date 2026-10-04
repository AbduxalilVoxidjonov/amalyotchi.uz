using SkiaSharp;

namespace Amaliyotchi.Infrastructure.Faces;

/// <summary>Rasmni (JPEG/PNG/WebP) SkiaSharp bilan o'qiydi, EXIF orientatsiyasini qo'llaydi (telefon rasmlari ko'pincha
/// "yonboshlab" saqlanadi) va ikki o'lchamga kichraytiradi: ishchi (hizalash uchun, uzun tomoni ≤ <c>workMax</c>) va
/// detektor uchun (≤ <c>detectMax</c>). O'qib bo'lmasa (HEIC, buzuq fayl) — null.</summary>
public static class SkiaImageLoader
{
    public sealed record Loaded(RgbImage Work, RgbImage Detect);

    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public static Loaded? Load(byte[] bytes, int workMax, int detectMax)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null)
            return null;

        // Katta rasmni decode paytida kichraytirish (JPEG 1/2, 1/4, 1/8) — xotira cho'qqisini kamaytiradi.
        var info = codec.Info;
        var longSide = Math.Max(info.Width, info.Height);
        var scale = longSide > workMax * 2 ? (float)(workMax * 2) / longSide : 1f;
        var size = scale < 1f ? codec.GetScaledDimensions(scale) : new SKSizeI(info.Width, info.Height);

        using var decoded = new SKBitmap(new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var result = codec.GetPixels(decoded.Info, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            return null;

        using var oriented = Orient(decoded, codec.EncodedOrigin);
        var source = oriented ?? decoded;

        using var work = Fit(source, workMax);
        using var detect = Fit(work ?? source, detectMax);
        return new Loaded(ToRgb(work ?? source), ToRgb(detect ?? work ?? source));
    }

    /// <summary>Uzun tomoni <paramref name="max"/> dan katta bo'lsa — proporsional kichraytirilgan nusxa, aks holda null.</summary>
    private static SKBitmap? Fit(SKBitmap source, int max)
    {
        var longSide = Math.Max(source.Width, source.Height);
        if (longSide <= max)
            return null;

        var ratio = (double)max / longSide;
        var width = Math.Max(1, (int)Math.Round(source.Width * ratio));
        var height = Math.Max(1, (int)Math.Round(source.Height * ratio));
        return source.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul), Sampling);
    }

    /// <summary>EXIF orientatsiyasini piksellarga qo'llaydi; TopLeft (o'zgarishsiz) — null.</summary>
    private static SKBitmap? Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default)
            return null;

        float w = source.Width, h = source.Height;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        // x' = ScaleX·x + SkewX·y + TransX; y' = SkewY·x + ScaleY·y + TransY.
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity
        };

        var target = new SKBitmap(new SKImageInfo(
            swap ? source.Height : source.Width, swap ? source.Width : source.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(target);
        canvas.Clear(SKColors.Black);
        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(source, 0, 0);
        canvas.Flush();
        return target;
    }

    private static RgbImage ToRgb(SKBitmap bitmap)
    {
        var rgba = bitmap.GetPixelSpan();
        var pixels = new byte[bitmap.Width * bitmap.Height * 3];
        for (int i = 0, j = 0; j < pixels.Length; i += 4, j += 3)
        {
            pixels[j] = rgba[i];
            pixels[j + 1] = rgba[i + 1];
            pixels[j + 2] = rgba[i + 2];
        }

        return new RgbImage(bitmap.Width, bitmap.Height, pixels);
    }
}

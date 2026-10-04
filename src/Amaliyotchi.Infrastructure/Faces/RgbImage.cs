namespace Amaliyotchi.Infrastructure.Faces;

/// <summary>Oddiy 8-bit RGB rasm (qator bo'yicha, piksel = R,G,B). Skia'dan mustaqil — hizalash va tensor tayyorlash
/// shu ustida (unit-test qilinadi).</summary>
public sealed class RgbImage
{
    public RgbImage(int width, int height, byte[] pixels)
    {
        if (width <= 0 || height <= 0 || pixels.Length != width * height * 3)
            throw new ArgumentException("Rasm o'lchami noto'g'ri.");
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    /// <summary>Bilinear namuna (OpenCV INTER_LINEAR kabi); chegaradan tashqari — 0 (BORDER_CONSTANT).</summary>
    public void Sample(float x, float y, Span<float> rgb)
    {
        rgb.Clear();
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var fx = x - x0;
        var fy = y - y0;
        Accumulate(x0, y0, (1 - fx) * (1 - fy), rgb);
        Accumulate(x0 + 1, y0, fx * (1 - fy), rgb);
        Accumulate(x0, y0 + 1, (1 - fx) * fy, rgb);
        Accumulate(x0 + 1, y0 + 1, fx * fy, rgb);
    }

    private void Accumulate(int x, int y, float weight, Span<float> rgb)
    {
        if (weight == 0 || x < 0 || y < 0 || x >= Width || y >= Height)
            return;
        var i = ((y * Width) + x) * 3;
        rgb[0] += Pixels[i] * weight;
        rgb[1] += Pixels[i + 1] * weight;
        rgb[2] += Pixels[i + 2] * weight;
    }
}

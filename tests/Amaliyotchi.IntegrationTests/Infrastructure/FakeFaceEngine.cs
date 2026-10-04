using System.Text;
using Amaliyotchi.Application.Common.Interfaces;

namespace Amaliyotchi.IntegrationTests.Infrastructure;

/// <summary>Testlarda haqiqiy YuNet/SFace o'rniga — deterministik: rasm bayt'lari ASCII "buyruq":
/// <list type="bullet">
/// <item><c>FACE:alice</c> — bitta yuz, "alice" embedding'i (har ism — o'z o'qidagi birlik vektor; turli ismlar → 0%);</item>
/// <item><c>FACE:alice~40</c> — bitta yuz, "alice" bilan aynan 40% o'xshash embedding;</item>
/// <item><c>FACES:2:alice</c> — 2 ta yuz (eng kattasi — alice);</item>
/// <item><c>UNREADABLE</c> — rasmni o'qib bo'lmaydi; boshqa har qanday bayt'lar — o'qiladi, yuz yo'q.</item>
/// </list>
/// <see cref="IsReady"/> — 503 testlari uchun o'chiriladi (<c>finally</c> da qaytariladi).</summary>
public sealed class FakeFaceEngine : IFaceEngine
{
    public const int Dimensions = 128;

    public bool IsReady { get; set; } = true;

    public string? UnavailableReason => IsReady ? null : "Test: modellar yo'q";

    public int Calls { get; private set; }

    public static byte[] Photo(string identity, int? similarityPercent = null)
        => Encoding.ASCII.GetBytes(similarityPercent is { } p ? $"FACE:{identity}~{p}" : $"FACE:{identity}");

    public static byte[] Group(int count, string identity) => Encoding.ASCII.GetBytes($"FACES:{count}:{identity}");

    public static readonly byte[] NoFace = Encoding.ASCII.GetBytes("LANDSCAPE");

    public static readonly byte[] Unreadable = Encoding.ASCII.GetBytes("UNREADABLE");

    public async Task<FaceAnalysis> AnalyzeAsync(Stream image, CancellationToken cancellationToken = default)
    {
        if (!IsReady)
            throw new InvalidOperationException("Test: modellar yo'q");

        Calls++;
        using var reader = new StreamReader(image, Encoding.ASCII);
        var text = await reader.ReadToEndAsync(cancellationToken);

        if (text == "UNREADABLE")
            return FaceAnalysis.Unreadable;
        if (text.StartsWith("FACES:", StringComparison.Ordinal))
        {
            var parts = text.Split(':', 3);
            return new FaceAnalysis(true, int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), Embedding(parts[2], null));
        }

        if (text.StartsWith("FACE:", StringComparison.Ordinal))
        {
            var spec = text["FACE:".Length..];
            var tilde = spec.IndexOf('~', StringComparison.Ordinal);
            return tilde < 0
                ? new FaceAnalysis(true, 1, Embedding(spec, null))
                : new FaceAnalysis(true, 1, Embedding(spec[..tilde], int.Parse(spec[(tilde + 1)..], System.Globalization.CultureInfo.InvariantCulture)));
        }

        return new FaceAnalysis(true, 0, null);
    }

    /// <summary>Ism → o'q indeksi; <paramref name="percent"/> berilsa — o'sha o'q bilan kosinusi percent/100 bo'lgan vektor.</summary>
    private static float[] Embedding(string identity, int? percent)
    {
        var axis = Axis(identity);
        var vector = new float[Dimensions];
        if (percent is not { } p)
        {
            vector[axis] = 1f;
            return vector;
        }

        var cos = p / 100.0;
        vector[axis] = (float)cos;
        vector[(axis + 1) % Dimensions] = (float)Math.Sqrt(1 - (cos * cos));
        return vector;
    }

    private static int Axis(string identity)
    {
        // Barqaror (jarayonlar orasida ham) hash; ikki qo'shni o'q band bo'lmasligi uchun juft indeks.
        var hash = 17;
        foreach (var ch in identity)
            hash = unchecked((hash * 31) + ch);
        return (int)((uint)hash % (Dimensions / 2)) * 2;
    }
}

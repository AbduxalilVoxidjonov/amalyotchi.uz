using Amaliyotchi.Domain.Faces;
using Amaliyotchi.Infrastructure.Faces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Amaliyotchi.UnitTests.Faces;

/// <summary>Haqiqiy YuNet + SFace modellari mavjud bo'lsa ishlaydi (<c>FACE_MODELS_PATH</c> yoki repodagi <c>.models/face</c>;
/// yuklash: <c>deploy/scripts/fetch-face-models.sh</c>), aks holda o'tkazib yuboriladi.</summary>
public sealed class FaceModelsFactAttribute : FactAttribute
{
    public FaceModelsFactAttribute()
    {
        if (FaceModels.Directory is null)
            Skip = "Yuz modellari topilmadi (deploy/scripts/fetch-face-models.sh) — real dvigatel testi o'tkazib yuborildi.";
    }
}

internal static class FaceModels
{
    public static string? Directory { get; } = Find();

    private static string? Find()
    {
        var fromEnv = Environment.GetEnvironmentVariable("FACE_MODELS_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return HasModels(fromEnv) ? fromEnv : null;

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, ".models", "face");
            if (HasModels(candidate))
                return candidate;
        }

        return null;
    }

    private static bool HasModels(string dir)
        => File.Exists(Path.Combine(dir, new FaceOptions().DetectorModel))
           && File.Exists(Path.Combine(dir, new FaceOptions().RecognizerModel));
}

public sealed class OnnxFaceEngineSmokeTests(ITestOutputHelper output) : IDisposable
{
    private readonly Lazy<OnnxFaceEngine> _engine = new(() => new OnnxFaceEngine(
        Options.Create(new FaceOptions { ModelsPath = FaceModels.Directory ?? "missing" }),
        NullLogger<OnnxFaceEngine>.Instance));

    private static readonly string Images = Path.Combine(AppContext.BaseDirectory, "TestData", "Faces");

    private async Task<Application.Common.Interfaces.FaceAnalysis> AnalyzeAsync(string file)
    {
        await using var stream = File.OpenRead(Path.Combine(Images, file));
        return await _engine.Value.AnalyzeAsync(stream);
    }

    [FaceModelsFact]
    public async Task BirOdam_Yuqori_BoshqaOdam_Past()
    {
        _engine.Value.IsReady.Should().BeTrue(_engine.Value.UnavailableReason);

        var collinsA = await AnalyzeAsync("collins-a.jpg");
        var collinsB = await AnalyzeAsync("collins-b.jpg");
        var williamsA = await AnalyzeAsync("williams-a.jpg");
        var williamsB = await AnalyzeAsync("williams-b.jpg");

        foreach (var a in new[] { collinsA, collinsB, williamsA, williamsB })
        {
            a.Readable.Should().BeTrue();
            a.FaceCount.Should().Be(1);
            a.Embedding.Should().HaveCount(128);
        }

        var same1 = FaceMatch.Score(collinsA.Embedding!, collinsB.Embedding!);
        var same2 = FaceMatch.Score(williamsA.Embedding!, williamsB.Embedding!);
        var diff1 = FaceMatch.Score(collinsA.Embedding!, williamsA.Embedding!);
        var diff2 = FaceMatch.Score(collinsB.Embedding!, williamsB.Embedding!);
        output.WriteLine($"Collins A↔B: {same1}%, Williams A↔B: {same2}%, Collins↔Williams: {diff1}%, {diff2}%");

        same1.Should().BeGreaterThanOrEqualTo(36);
        same2.Should().BeGreaterThanOrEqualTo(36);
        diff1.Should().BeLessThan(36);
        diff2.Should().BeLessThan(36);
    }

    [FaceModelsFact]
    public async Task GuruhRasmi_BirNechtaYuz()
    {
        var group = await AnalyzeAsync("crew-group.jpg");
        output.WriteLine($"Guruh rasmidagi yuzlar: {group.FaceCount}");
        group.FaceCount.Should().BeGreaterThan(1);
        group.Embedding.Should().NotBeNull("eng katta yuz embedding'i qaytadi");
    }

    [FaceModelsFact]
    public async Task YuzsizVaBuzuqRasm()
    {
        await using (var notImage = new MemoryStream("salom"u8.ToArray()))
            (await _engine.Value.AnalyzeAsync(notImage)).Readable.Should().BeFalse();

        // Bir xil kulrang PNG — yuz yo'q.
        using var bitmap = new SkiaSharp.SKBitmap(320, 240);
        bitmap.Erase(new SkiaSharp.SKColor(128, 128, 128));
        using var data = SkiaSharp.SKImage.FromBitmap(bitmap).Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        await using var gray = new MemoryStream(data.ToArray());
        var result = await _engine.Value.AnalyzeAsync(gray);
        result.Readable.Should().BeTrue();
        result.FaceCount.Should().Be(0);
        result.Embedding.Should().BeNull();
    }

    [Fact]
    public void ModellarYoq_IsReadyFalse_SababBilan()
    {
        using var engine = new OnnxFaceEngine(
            Options.Create(new FaceOptions { ModelsPath = Path.Combine(Path.GetTempPath(), "yoq-" + Guid.NewGuid()) }),
            NullLogger<OnnxFaceEngine>.Instance);
        engine.IsReady.Should().BeFalse();
        engine.UnavailableReason.Should().Contain("topilmadi");
        engine.Invoking(e => e.AnalyzeAsync(new MemoryStream())).Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        if (_engine.IsValueCreated)
            _engine.Value.Dispose();
    }
}

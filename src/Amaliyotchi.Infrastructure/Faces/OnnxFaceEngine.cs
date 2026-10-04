using Amaliyotchi.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;

namespace Amaliyotchi.Infrastructure.Faces;

/// <summary><see cref="IFaceEngine"/> — jarayon ichida, bulutsiz: OpenCV Zoo <b>YuNet</b> (detektor, MIT) +
/// <b>SFace</b> (embedding, Apache-2.0) ONNX modellari <c>Microsoft.ML.OnnxRuntime</c> (MIT) orqali; rasm decode —
/// SkiaSharp (MIT). Oqim: decode + EXIF → 640×640 letterbox (BGR, 0..255) → YuNet → NMS → eng katta yuz →
/// 5 nuqta bo'yicha 112×112 hizalash (RGB, 0..255) → SFace → 128 son. OpenCV <c>FaceDetectorYN</c>/<c>FaceRecognizerSF</c>
/// bilan bir xil pre/post-processing.
/// Modellar topilmasa yoki yuklanmasa <see cref="IsReady"/> = false (startup'da ogohlantirish) — ilova ishlayveradi,
/// yuzni tasdiqlash yoqilgan bo'lsa tegishli so'rovlar 503 oladi.</summary>
public sealed class OnnxFaceEngine : IFaceEngine, IDisposable
{
    private const int DetectorInput = 640;
    private const int WorkMaxSide = 1024;
    private const int EmbeddingSize = 128;

    private static readonly string[] DetectorOutputs =
    [
        "cls_8", "cls_16", "cls_32", "obj_8", "obj_16", "obj_32",
        "bbox_8", "bbox_16", "bbox_32", "kps_8", "kps_16", "kps_32"
    ];

    private readonly FaceOptions _options;
    private readonly ILogger<OnnxFaceEngine> _logger;
    private readonly InferenceSession? _detector;
    private readonly InferenceSession? _recognizer;
    private readonly SemaphoreSlim _gate;

    public OnnxFaceEngine(IOptions<FaceOptions> options, ILogger<OnnxFaceEngine> logger)
    {
        _options = options.Value;
        _logger = logger;
        _gate = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrency));

        var directory = ResolveModelsPath(_options.ModelsPath);
        var detectorPath = directory is null ? null : Path.Combine(directory, _options.DetectorModel);
        var recognizerPath = directory is null ? null : Path.Combine(directory, _options.RecognizerModel);

        if (detectorPath is null || recognizerPath is null || !File.Exists(detectorPath) || !File.Exists(recognizerPath))
        {
            UnavailableReason = $"Yuz modellari topilmadi (Face:ModelsPath = '{_options.ModelsPath}'). " +
                                "deploy/scripts/fetch-face-models.sh bilan yuklang.";
            return;
        }

        try
        {
            using var sessionOptions = new SessionOptions
            {
                IntraOpNumThreads = Math.Max(1, _options.IntraOpThreads),
                InterOpNumThreads = 1,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };
            _detector = new InferenceSession(detectorPath, sessionOptions);
            _recognizer = new InferenceSession(recognizerPath, sessionOptions);
            ModelsDirectory = directory;
        }
        catch (Exception ex) when (ex is OnnxRuntimeException or IOException or DllNotFoundException or TypeInitializationException)
        {
            _detector?.Dispose();
            _detector = null;
            _recognizer = null;
            UnavailableReason = $"Yuz modellarini yuklab bo'lmadi: {ex.Message}";
            _logger.LogError(ex, "Yuz modellarini yuklab bo'lmadi ({Path})", directory);
        }
    }

    public bool IsReady => _detector is not null && _recognizer is not null;

    public string? UnavailableReason { get; }

    /// <summary>Modellar yuklangan papka (log uchun).</summary>
    public string? ModelsDirectory { get; }

    public async Task<FaceAnalysis> AnalyzeAsync(Stream image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (!IsReady)
            throw new InvalidOperationException(UnavailableReason ?? "Yuz dvigateli tayyor emas.");

        using var buffer = new MemoryStream();
        await image.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() => Analyze(bytes), cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Barcha topilgan yuzlar (ishchi rasm koordinatalarida) — diagnostika va testlar uchun.</summary>
    public IReadOnlyList<DetectedFace> Detect(RgbImage detect, float toWorkScale)
    {
        var tensor = new float[3 * DetectorInput * DetectorInput];
        const int plane = DetectorInput * DetectorInput;
        var px = detect.Pixels;
        for (var y = 0; y < detect.Height; y++)
        {
            for (var x = 0; x < detect.Width; x++)
            {
                var i = ((y * detect.Width) + x) * 3;
                var o = (y * DetectorInput) + x;
                // OpenCV blobFromImage (swapRB=false): BGR tartibi, 0..255; qolgan joy — 0 (o'ng/past padding).
                tensor[o] = px[i + 2];
                tensor[plane + o] = px[i + 1];
                tensor[(2 * plane) + o] = px[i];
            }
        }

        using var input = OrtValue.CreateTensorValueFromMemory(tensor, [1, 3, DetectorInput, DetectorInput]);
        using var runOptions = new RunOptions();
        using var outputs = _detector!.Run(runOptions, [_detector.InputNames[0]], [input], DetectorOutputs);

        float[] At(int i) => outputs[i].GetTensorDataAsSpan<float>().ToArray();
        var faces = YuNetDecoder.Decode(
            DetectorInput, DetectorInput,
            [At(0), At(1), At(2)], [At(3), At(4), At(5)], [At(6), At(7), At(8)], [At(9), At(10), At(11)],
            _options.DetectionThreshold, _options.NmsThreshold);

        return faces.Select(f => f.Scale(toWorkScale)).ToList();
    }

    public float[] Embed(RgbImage work, DetectedFace face)
    {
        var tensor = FaceAligner.AlignToTensor(work, face.Landmarks);
        using var input = OrtValue.CreateTensorValueFromMemory(tensor, [1, 3, FaceAligner.Size, FaceAligner.Size]);
        using var runOptions = new RunOptions();
        using var outputs = _recognizer!.Run(runOptions, ["data"], [input], [_recognizer.OutputNames[0]]);
        var embedding = outputs[0].GetTensorDataAsSpan<float>().ToArray();
        if (embedding.Length != EmbeddingSize)
            throw new InvalidOperationException($"SFace embedding o'lchami kutilmagan: {embedding.Length}.");
        return embedding;
    }

    private FaceAnalysis Analyze(byte[] bytes)
    {
        SkiaImageLoader.Loaded? loaded;
        try
        {
            loaded = SkiaImageLoader.Load(bytes, WorkMaxSide, DetectorInput);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            _logger.LogInformation(ex, "Rasmni o'qib bo'lmadi");
            return FaceAnalysis.Unreadable;
        }

        if (loaded is null)
            return FaceAnalysis.Unreadable;

        var faces = Detect(loaded.Detect, (float)loaded.Work.Width / loaded.Detect.Width);
        if (faces.Count == 0)
            return new FaceAnalysis(true, 0, null);

        var largest = faces.MaxBy(f => f.Area)!;
        return new FaceAnalysis(true, faces.Count, Embed(loaded.Work, largest));
    }

    /// <summary>Nisbiy yo'l: avval ilova papkasi (Docker: /app), keyin joriy papka (lokal <c>dotnet run</c>).</summary>
    private static string? ResolveModelsPath(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return null;
        if (Path.IsPathRooted(configured))
            return configured;

        string[] candidates =
        [
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured)),
            Path.GetFullPath(configured)
        ];
        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    public void Dispose()
    {
        _detector?.Dispose();
        _recognizer?.Dispose();
        _gate.Dispose();
    }
}

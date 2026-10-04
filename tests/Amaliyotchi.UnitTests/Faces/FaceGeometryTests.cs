using Amaliyotchi.Infrastructure.Faces;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Faces;

public sealed class FaceGeometryTests
{
    [Fact]
    public void Similarity_ShablonningOzi_Identity()
    {
        var (a, b, tx, ty) = FaceAligner.EstimateSimilarity(FaceAligner.Template, FaceAligner.Template);
        a.Should().BeApproximately(1, 1e-9);
        b.Should().BeApproximately(0, 1e-9);
        tx.Should().BeApproximately(0, 1e-6);
        ty.Should().BeApproximately(0, 1e-6);
    }

    [Fact]
    public void Similarity_IkkiBarobarVaSiljigan_Topiladi()
    {
        // src = template·2 + (10, 20) → src→dst: a = 0.5, siljish −(5, 10).
        var src = FaceAligner.Template.Select((v, i) => (v * 2) + (i % 2 == 0 ? 10 : 20)).ToArray();
        var (a, b, tx, ty) = FaceAligner.EstimateSimilarity(src, FaceAligner.Template);
        a.Should().BeApproximately(0.5, 1e-9);
        b.Should().BeApproximately(0, 1e-9);
        tx.Should().BeApproximately(-5, 1e-6);
        ty.Should().BeApproximately(-10, 1e-6);
    }

    [Fact]
    public void Align_Identity_PiksellarniKochiradi_RGBTartibda()
    {
        var pixels = new byte[112 * 112 * 3];
        for (var i = 0; i < pixels.Length; i += 3)
        {
            pixels[i] = 10;
            pixels[i + 1] = 20;
            pixels[i + 2] = 30;
        }

        var tensor = FaceAligner.AlignToTensor(new RgbImage(112, 112, pixels), FaceAligner.Template);
        tensor.Should().HaveCount(3 * 112 * 112);
        tensor[(56 * 112) + 56].Should().BeApproximately(10, 1e-3f);
        tensor[(112 * 112) + (56 * 112) + 56].Should().BeApproximately(20, 1e-3f);
        tensor[(2 * 112 * 112) + (56 * 112) + 56].Should().BeApproximately(30, 1e-3f);
    }

    [Fact]
    public void Decode_BittaKatak_MarkazVaNuqtalar()
    {
        // 640×640, stride 32 → 20×20 katak; (r=2, c=3) katagida ishonchli yuz.
        float[][] cls = [new float[6400], new float[1600], new float[400]];
        float[][] obj = [new float[6400], new float[1600], new float[400]];
        float[][] bbox = [new float[6400 * 4], new float[1600 * 4], new float[400 * 4]];
        float[][] kps = [new float[6400 * 10], new float[1600 * 10], new float[400 * 10]];
        var idx = (2 * 20) + 3;
        cls[2][idx] = 0.95f;
        obj[2][idx] = 1.2f; // 1 ga qisiladi
        bbox[2][(idx * 4) + 0] = 0.5f;
        bbox[2][(idx * 4) + 1] = 0.5f;
        bbox[2][(idx * 4) + 2] = 0f; // exp(0)·32 = 32
        bbox[2][(idx * 4) + 3] = MathF.Log(2); // 64
        kps[2][(idx * 10) + 0] = 0.25f;
        kps[2][(idx * 10) + 1] = 0.75f;

        var faces = YuNetDecoder.Decode(640, 640, cls, obj, bbox, kps, 0.9f, 0.3f);

        faces.Should().ContainSingle();
        var f = faces[0];
        f.Score.Should().BeApproximately(MathF.Sqrt(0.95f), 1e-5f);
        f.X.Should().BeApproximately(((3 + 0.5f) * 32) - 16, 1e-3f);
        f.Y.Should().BeApproximately(((2 + 0.5f) * 32) - 32, 1e-3f);
        f.Width.Should().BeApproximately(32, 1e-3f);
        f.Height.Should().BeApproximately(64, 1e-3f);
        f.Landmarks[0].Should().BeApproximately(3.25f * 32, 1e-3f);
        f.Landmarks[1].Should().BeApproximately(2.75f * 32, 1e-3f);
    }

    [Fact]
    public void Nms_UstmaUstQutilar_BittasiQoladi()
    {
        var a = new DetectedFace(0, 0, 100, 100, 0.95f, new float[10]);
        var b = new DetectedFace(5, 5, 100, 100, 0.92f, new float[10]);
        var c = new DetectedFace(300, 300, 50, 50, 0.91f, new float[10]);
        YuNetDecoder.Nms([b, a, c], 0.3f).Should().Equal(a, c);
    }
}

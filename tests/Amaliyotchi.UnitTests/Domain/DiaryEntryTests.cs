using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

public sealed class DiaryEntryTests
{
    private static readonly string LongText = new('a', 200);
    private static readonly Guid Tutor = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static DiaryEntry Submitted()
        => DiaryEntry.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), new DateOnly(2026, 10, 5), LongText, null, Now);

    [Fact]
    public void QisqaMatn_XatoBeradi()
    {
        var act = () => DiaryEntry.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), new DateOnly(2026, 10, 5), "qisqa", null, Now);

        act.Should().Throw<DomainException>().WithMessage("*150*");
    }

    [Fact]
    public void MinimalUzunlik_SozlamadanKeladi()
    {
        var entry = DiaryEntry.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), new DateOnly(2026, 10, 5), "qisqa hisobot", null, Now, minTextLength: 5);

        entry.Status.Should().Be(DiaryStatus.Submitted);
    }

    [Fact]
    public void MarkSeen_Approve_BallBilan()
    {
        var entry = Submitted();

        entry.MarkSeen(Tutor, Now);
        entry.Status.Should().Be(DiaryStatus.Seen);

        entry.Approve(Tutor, 5, "Zo'r", Now);

        entry.Status.Should().Be(DiaryStatus.Approved);
        entry.Score.Should().Be(5);
        entry.TutorComment.Should().Be("Zo'r");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Approve_NotogriBall_XatoBeradi(int score)
    {
        var act = () => Submitted().Approve(Tutor, score, null, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Tasdiqlangan_QaytaKoribChiqilmaydi()
    {
        var entry = Submitted();
        entry.Approve(Tutor, 4, null, Now);

        var act = () => entry.RequestRewrite(Tutor, "qayta", Now);

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void Rewrite_Resubmit_YanaYuborilgan()
    {
        var entry = Submitted();
        entry.RequestRewrite(Tutor, "Batafsilroq yozing", Now);
        entry.Status.Should().Be(DiaryStatus.Rewrite);

        entry.Resubmit(LongText + "!", "yangilik", Now.AddHours(1));

        entry.Status.Should().Be(DiaryStatus.Submitted);
        entry.Learned.Should().Be("yangilik");
        entry.Score.Should().BeNull();
    }

    [Fact]
    public void Resubmit_FaqatRewriteHolatidan()
    {
        var act = () => Submitted().Resubmit(LongText, null, Now);

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void Fayllar_BeshtadanOshmaydi()
    {
        var entry = Submitted();
        for (var i = 0; i < DiaryEntry.MaxAttachments; i++)
            entry.AddAttachment(Guid.CreateVersion7(), $"rasm{i}.jpg", 1024);

        var act = () => entry.AddAttachment(Guid.CreateVersion7(), "ortiqcha.jpg", 1024);

        act.Should().Throw<DomainException>();
        entry.Attachments.Should().HaveCount(DiaryEntry.MaxAttachments);
    }
}

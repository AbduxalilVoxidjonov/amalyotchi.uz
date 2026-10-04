using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Faces;
using Amaliyotchi.Domain.Settings;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Faces;

public sealed class FaceMatchTests
{
    [Fact]
    public void BirXilVektor_100()
        => FaceMatch.Score([1f, 2f, 3f], [2f, 4f, 6f]).Should().Be(100);

    [Fact]
    public void Ortogonal_0_Manfiy_0()
    {
        FaceMatch.Score([1f, 0f], [0f, 1f]).Should().Be(0);
        FaceMatch.Score([1f, 0f], [-1f, 0f]).Should().Be(0, "manfiy kosinus 0 ga qisiladi");
    }

    [Fact]
    public void Yaxlitlash_FoizgaAylantiriladi()
    {
        // cos = 0.6 → 60; cos(a, b) = 0.3634 → 36.
        FaceMatch.Score([1f, 0f], [0.6f, 0.8f]).Should().Be(60);
        var angle = Math.Acos(0.3634);
        FaceMatch.Score([1f, 0f], [(float)Math.Cos(angle), (float)Math.Sin(angle)]).Should().Be(36);
    }

    [Theory]
    [InlineData(36, 36, true)]
    [InlineData(35, 36, false)]
    [InlineData(100, 100, true)]
    [InlineData(0, 0, true)]
    public void Chegara_TengBolsa_Mos(int score, int threshold, bool expected)
        => FaceMatch.IsMatch(score, threshold).Should().Be(expected);

    [Fact]
    public void OlchamMosEmas_Xato()
    {
        var act = () => FaceMatch.Score([1f, 2f], [1f]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NolVektor_0()
        => FaceMatch.Score([0f, 0f], [1f, 1f]).Should().Be(0);

    [Fact]
    public void Sozlamalar_StandartQiymatlar()
    {
        SettingKeys.Get(SettingKeys.FaceVerificationEnabled).DefaultValue.Should().Be("false");
        var threshold = SettingKeys.Get(SettingKeys.FaceMatchThreshold);
        threshold.DefaultValue.Should().Be("36");
        threshold.Validate("40").Should().Be("40");
        threshold.Invoking(t => t.Validate("101")).Should().Throw<DomainException>();
    }
}

public sealed class StudentFaceEnrollmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Student = Guid.CreateVersion7();
    private static readonly Guid Tutor = Guid.CreateVersion7();

    private static StudentFaceEnrollment Pending()
        => StudentFaceEnrollment.Submit(Student, Guid.CreateVersion7(), [0.1f, 0.2f], Now);

    [Fact]
    public void Submit_Pending_RozilikVaqtiBilan()
    {
        var e = Pending();
        e.Status.Should().Be(FaceEnrollmentStatus.Pending);
        e.ConsentAt.Should().Be(Now);
        e.SubmittedAt.Should().Be(Now);
        e.IsUsableReference.Should().BeTrue();
    }

    [Fact]
    public void Approve_FaqatPending()
    {
        var e = Pending();
        e.Approve(Tutor, Now.AddHours(1));
        e.Status.Should().Be(FaceEnrollmentStatus.Approved);
        e.ReviewedByUserId.Should().Be(Tutor);
        e.IsUsableReference.Should().BeTrue();

        e.Invoking(x => x.Approve(Tutor, Now)).Should().Throw<ConflictException>();
    }

    [Fact]
    public void Reject_SababMajburiy_KeyinQaytaYuborish()
    {
        var e = Pending();
        e.Invoking(x => x.Reject(Tutor, "  ", Now)).Should().Throw<DomainException>();
        e.Invoking(x => x.Reject(Tutor, new string('a', 501), Now)).Should().Throw<DomainException>();

        e.Reject(Tutor, " Yuz ko'rinmaydi ", Now);
        e.Status.Should().Be(FaceEnrollmentStatus.Rejected);
        e.RejectReason.Should().Be("Yuz ko'rinmaydi");
        e.IsUsableReference.Should().BeFalse();
        e.Invoking(x => x.Reject(Tutor, "yana", Now)).Should().Throw<ConflictException>();
        e.Invoking(x => x.Approve(Tutor, Now)).Should().Throw<ConflictException>();

        var photo = Guid.CreateVersion7();
        e.Resubmit(photo, [1f], Now.AddDays(1));
        e.Status.Should().Be(FaceEnrollmentStatus.Pending);
        e.PhotoFileId.Should().Be(photo);
        e.RejectReason.Should().BeNull();
        e.ReviewedAt.Should().BeNull();
    }

    [Fact]
    public void Tasdiqlangan_TalabaAlmashtiraOlmaydi()
    {
        var e = Pending();
        e.Approve(Tutor, Now);
        e.Invoking(x => x.Resubmit(Guid.CreateVersion7(), [1f], Now))
            .Should().Throw<ConflictException>().WithMessage(StudentFaceEnrollment.AlreadyApprovedMessage);
    }

    [Fact]
    public void Tasdiqlangan_RadEtilishiMumkin()
    {
        var e = Pending();
        e.Approve(Tutor, Now);
        e.Reject(Tutor, "Boshqa odam", Now);
        e.Status.Should().Be(FaceEnrollmentStatus.Rejected);
    }

    [Fact]
    public void BoshEmbedding_Xato()
        => FluentActions.Invoking(() => StudentFaceEnrollment.Submit(Student, Guid.CreateVersion7(), [], Now))
            .Should().Throw<DomainException>();
}

public sealed class FaceRejectReasonTests
{
    [Fact]
    public void FaceMismatch_XabardaBall_KengaytmadaKod()
    {
        var verdict = CheckInVerdict.Reject(CheckInRejectReason.FaceMismatch) with { FaceMatchScore = 21 };
        var ex = verdict.ToException();

        ex.Should().NotBeOfType<ConflictException>("yuz sabablari 400");
        ex.Message.Should().Be("Yuz etalonga mos kelmadi (21%). Qayta suratga oling.");
        ex.Extensions["rejectReason"].Should().Be("faceMismatch");
        ex.Extensions["faceMatchScore"].Should().Be(21);
    }

    [Theory]
    [InlineData(CheckInRejectReason.FaceNotEnrolled, "faceNotEnrolled", "Avval yuzingizni tasdiqlang.")]
    [InlineData(CheckInRejectReason.FaceNotDetected, "faceNotDetected", "Rasmda yuz topilmadi. Qayta suratga oling.")]
    public void YuzSabablari(CheckInRejectReason reason, string code, string message)
    {
        var ex = reason.ToException();
        ex.Message.Should().Be(message);
        ex.Extensions["rejectReason"].Should().Be(code);
        ex.Extensions.Should().NotContainKey("faceMatchScore");
    }

    [Fact]
    public void MavjudSabablar_HamKodOladi_409SaqlanadI()
    {
        var ex = CheckInRejectReason.OutOfRadius.ToException();
        ex.Should().BeOfType<ConflictException>();
        ex.Extensions["rejectReason"].Should().Be("outOfRadius");
    }

    [Fact]
    public void QabulQilinganCheckIn_BallDavomatgaYoziladi()
    {
        var verdict = CheckInVerdict.Accept() with { FaceMatchScore = 77 };
        var attendance = DailyAttendance.CheckIn(
            Guid.CreateVersion7(), Guid.CreateVersion7(), new DateOnly(2026, 10, 3), DateTimeOffset.UtcNow, 10, 5, verdict);
        attendance.FaceMatchScore.Should().Be(77);
    }
}

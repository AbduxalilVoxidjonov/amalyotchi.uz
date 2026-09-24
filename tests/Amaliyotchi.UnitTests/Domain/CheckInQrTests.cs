using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

/// <summary>Korxona check-in QR kodi: token formati, payload (<c>AMLQR:1:{token}</c>), almashtirish, moslik va
/// policy'dagi o'rni (oynadan keyin, GPS/radiusdan oldin).</summary>
public sealed class CheckInQrTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 4, 0, 0, TimeSpan.Zero);

    private static Company Create(DateTimeOffset? at = null)
        => Company.Create("UzInfoCom", "123456789", "IT", "Toshkent, Amir Temur 1", new GeoPoint(41.31, 69.24), 200,
            "Islomov B.", "90 123 45 67", createdAt: at ?? Now);

    [Fact]
    public void Yaratilganda_Token32BelgiliKichikHex_VaqtYoziladi()
    {
        var company = Create();

        company.CheckInQrToken.Should().HaveLength(32).And.MatchRegex("^[0-9a-f]{32}$");
        company.CheckInQrRotatedAt.Should().Be(Now);
        company.CheckInQrPayload.Should().Be("AMLQR:1:" + company.CheckInQrToken);
    }

    [Fact]
    public void HarKorxonaningTokeniAlohida()
    {
        var tokens = Enumerable.Range(0, 50).Select(_ => Create().CheckInQrToken).ToList();

        tokens.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Rotate_YangiToken_EskiQrYaroqsiz()
    {
        var company = Create();
        var oldPayload = company.CheckInQrPayload;
        var later = Now.AddDays(3);

        company.RotateCheckInQr(later);

        company.CheckInQrToken.Should().NotBe(oldPayload[CheckInQr.Prefix.Length..]).And.MatchRegex("^[0-9a-f]{32}$");
        company.CheckInQrRotatedAt.Should().Be(later);
        CheckInQr.Matches(oldPayload, company.CheckInQrToken).Should().BeFalse();
        CheckInQr.Matches(company.CheckInQrPayload, company.CheckInQrToken).Should().BeTrue();
    }

    [Fact]
    public void Matches_ToGriPayload_ChetBoshliqVaKattaHarfKechiriladi()
    {
        var company = Create();

        CheckInQr.Matches(company.CheckInQrPayload, company.CheckInQrToken).Should().BeTrue();
        CheckInQr.Matches($"  {company.CheckInQrPayload}\n", company.CheckInQrToken).Should().BeTrue();
        CheckInQr.Matches(CheckInQr.Prefix + company.CheckInQrToken.ToUpperInvariant(), company.CheckInQrToken).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0123456789abcdef0123456789abcdef")] // prefikssiz
    [InlineData("AMLQR:2:0123456789abcdef0123456789abcdef")] // boshqa versiya
    [InlineData("amlqr:1:0123456789abcdef0123456789abcdef")] // prefiks registri
    [InlineData("AMLQR:1:0123456789abcdef0123456789abcde")] // 31 belgi
    [InlineData("AMLQR:1:0123456789abcdef0123456789abcdef0")] // 33 belgi
    [InlineData("AMLQR:1:0123456789abcdef0123456789abcdeg")] // hex emas
    [InlineData("https://example.com/AMLQR:1:0123456789abcdef0123456789abcdef")]
    public void TryParse_NotogriFormat_False(string? payload)
    {
        CheckInQr.TryParse(payload, out var token).Should().BeFalse();
        token.Should().BeEmpty();
        CheckInQr.Matches(payload, "0123456789abcdef0123456789abcdef").Should().BeFalse();
    }

    [Fact]
    public void Matches_BoshqaKorxonaQri_False()
    {
        var mine = Create();
        var other = Create();

        CheckInQr.Matches(other.CheckInQrPayload, mine.CheckInQrToken).Should().BeFalse();
    }

    [Fact]
    public void QrInvalid_Xabar_Va409()
    {
        CheckInRejectReason.QrInvalid.Should().Be((CheckInRejectReason)13);
        CheckInRejectReason.QrInvalid.Message().Should().Be("QR kod bu amaliyot joyiga tegishli emas.");
        CheckInRejectReason.QrInvalid.ToException().Should().BeOfType<ConflictException>();
    }

    [Fact]
    public void Sozlama_CheckInQrRequired_MantiqiyVaStandartYoqilgan()
    {
        var definition = SettingKeys.Get(SettingKeys.CheckInQrRequired);

        SettingKeys.CheckInQrRequired.Should().Be("checkinQrRequired");
        definition.Type.Should().Be(SettingType.Bool);
        definition.DefaultValue.Should().Be("true");
        definition.Note.Should().Be("Yoqilgan bo'lsa check-in/check-out uchun korxona QR kodi skanerlanishi shart.");
    }

    // ---- Policy tartibi ----

    private static CheckInContext InCtx(TimeOnly now, bool qrValid) => new(
        Date: new DateOnly(2026, 9, 14),
        LocalNow: now,
        ApplicationApproved: true,
        PeriodStarted: true,
        PeriodEnded: false,
        WorkDays: WorkDays.MondayToSaturday,
        IsHoliday: false,
        HasApprovedLeave: false,
        AlreadyCheckedIn: false,
        AccuracyM: 20,
        DistanceM: 45,
        RadiusM: 200,
        QrValid: qrValid);

    [Fact]
    public void Policy_QrNotogri_RadEtiladi_RadiusVaGpsdanOldin()
    {
        var verdict = CheckInPolicy.Evaluate(
            InCtx(new TimeOnly(9, 5), qrValid: false) with { DistanceM = 5000, AccuracyM = 900 }, CheckInRules.Default);

        verdict.Accepted.Should().BeFalse();
        verdict.Reason.Should().Be(CheckInRejectReason.QrInvalid);
    }

    [Fact]
    public void Policy_OynaYopiq_QrdanUstun()
    {
        var verdict = CheckInPolicy.Evaluate(InCtx(new TimeOnly(11, 0), qrValid: false), CheckInRules.Default);

        verdict.Reason.Should().Be(CheckInRejectReason.WindowClosed);
    }

    [Fact]
    public void Policy_ArizaTasdiqlanmagan_QrdanUstun()
    {
        var verdict = CheckInPolicy.Evaluate(
            InCtx(new TimeOnly(9, 5), qrValid: false) with { ApplicationApproved = false }, CheckInRules.Default);

        verdict.Reason.Should().Be(CheckInRejectReason.NotApproved);
    }

    [Fact]
    public void Policy_QrToGri_QabulQilinadi()
    {
        CheckInPolicy.Evaluate(InCtx(new TimeOnly(9, 5), qrValid: true), CheckInRules.Default).Accepted.Should().BeTrue();
    }

    [Fact]
    public void CheckOutPolicy_QrNotogri_RadEtiladi_CheckInYoqBolsaAvvalShu()
    {
        var ctx = new CheckOutContext(new TimeOnly(17, 5), true, false, false, 20, 5000, 200, QrValid: false);

        CheckInPolicy.EvaluateCheckOut(ctx, CheckInRules.Default).Reason.Should().Be(CheckInRejectReason.QrInvalid);
        CheckInPolicy.EvaluateCheckOut(ctx with { HasCheckedIn = false }, CheckInRules.Default)
            .Reason.Should().Be(CheckInRejectReason.NoCheckIn);
        CheckInPolicy.EvaluateCheckOut(ctx with { QrValid = true, DistanceM = 45 }, CheckInRules.Default)
            .Accepted.Should().BeTrue();
    }
}

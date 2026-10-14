using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.Domain.Students;
using Amaliyotchi.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

/// <summary>Live joylashuv qoidalari (docs/LIVE-JOYLASHUV.md): ping holati, outsideMinutes (oraliq kesish),
/// signalLost, presence holati, idempotentlik kaliti, rozilik, sozlamalar.</summary>
public sealed class LocationTrackingTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 4, 0, 0, TimeSpan.Zero); // 09:00 Toshkent
    private const int Interval = 300;

    private static PingSample P(int minute, LocationPingStatus status, double distance = 10, bool mocked = false)
        => new(T0.AddMinutes(minute), status, distance, mocked);

    [Theory]
    [InlineData(10, 50, 150, LocationPingStatus.Inside)]
    [InlineData(10, 150, 150, LocationPingStatus.Inside)]     // chegara talaba foydasiga
    [InlineData(10, 150.1, 150, LocationPingStatus.Outside)]
    [InlineData(100, 400, 150, LocationPingStatus.Outside)]   // aniqlik = chegara — qabul
    [InlineData(100.5, 20, 150, LocationPingStatus.Inaccurate)]
    [InlineData(250, 5000, 150, LocationPingStatus.Inaccurate)] // yomon aniqlik — na ichkari, na tashqari
    public void Classify_CheckInGeozonaQoidasi(double accuracy, double distance, int radius, LocationPingStatus expected)
        => LocationTracking.Classify(accuracy, distance, radius, minAccuracyM: 100).Should().Be(expected);

    [Fact]
    public void OutsideMinutes_FaqatOldingiPingTashqaridaBolsaQoshiladi()
    {
        // 0 in → 5 out → 10 out → 15 in → 20 out (oxirgi; undan keyingi vaqt hisoblanmaydi)
        var pings = new[]
        {
            P(0, LocationPingStatus.Inside), P(5, LocationPingStatus.Outside), P(10, LocationPingStatus.Outside),
            P(15, LocationPingStatus.Inside), P(20, LocationPingStatus.Outside)
        };

        LocationTracking.OutsideMinutes(pings, Interval).Should().Be(10);
    }

    [Fact]
    public void OutsideMinutes_UzunOraliq2xIntervaldaKesiladi()
    {
        // outside 0 → 40 daqiqa signal yo'q → 40: oraliq 2 × 300 s = 10 daqiqada kesiladi.
        var pings = new[] { P(0, LocationPingStatus.Outside), P(40, LocationPingStatus.Outside), P(45, LocationPingStatus.Inside) };

        LocationTracking.OutsideMinutes(pings, Interval).Should().Be(10 + 5);
    }

    [Fact]
    public void OutsideMinutes_InaccurateVaInsideHisoblanmaydi_BoshRoyxat0()
    {
        LocationTracking.OutsideMinutes([], Interval).Should().Be(0);
        LocationTracking.OutsideMinutes([P(0, LocationPingStatus.Outside)], Interval).Should().Be(0);
        LocationTracking.OutsideMinutes(
            [P(0, LocationPingStatus.Inaccurate), P(5, LocationPingStatus.Inside), P(10, LocationPingStatus.Outside)], Interval)
            .Should().Be(0);
    }

    [Fact]
    public void LongestGap_KesilmaganEngUzunOraliq()
    {
        LocationTracking.LongestGapMinutes([P(0, LocationPingStatus.Inside), P(5, LocationPingStatus.Inside), P(27, LocationPingStatus.Outside)])
            .Should().Be(22);
        LocationTracking.LongestGapMinutes([P(0, LocationPingStatus.Inside)]).Should().Be(0);
    }

    [Theory]
    [InlineData(15, false)]  // aynan 3 × 300 s — hali signal bor
    [InlineData(16, true)]
    public void SignalLost_3xIntervaldanEski(int minutesSinceLast, bool expected)
        => LocationTracking.IsSignalLost(T0, T0.AddMinutes(minutesSinceLast), Interval).Should().Be(expected);

    private static PresenceInput Input(
        IReadOnlyList<PingSample> pings, int nowMinute, bool consent = true, DateTimeOffset? checkOut = null, int autoCloseMinute = 600)
        => new(T0, checkOut, T0.AddMinutes(autoCloseMinute), 25, consent, pings, T0.AddMinutes(nowMinute), Interval);

    [Fact]
    public void Presence_OxirgiPingHolati_MasofaVaMocked()
    {
        var pings = new[]
        {
            P(5, LocationPingStatus.Inside, 30), P(10, LocationPingStatus.Outside, 320, mocked: true), P(15, LocationPingStatus.Outside, 410)
        };

        var result = LocationTracking.Evaluate(Input(pings, nowMinute: 17));

        result.State.Should().Be(PresenceState.Outside);
        result.LastPingAt.Should().Be(T0.AddMinutes(15));
        result.DistanceM.Should().Be(410);
        result.OutsideMinutes.Should().Be(5);
        result.Mocked.Should().BeTrue();
    }

    [Fact]
    public void Presence_Inaccurate()
        => LocationTracking.Evaluate(Input([P(5, LocationPingStatus.Inaccurate)], 6)).State.Should().Be(PresenceState.Inaccurate);

    [Fact]
    public void Presence_OxirgiPingEski_SignalLost()
        => LocationTracking.Evaluate(Input([P(5, LocationPingStatus.Inside)], nowMinute: 21)).State.Should().Be(PresenceState.SignalLost);

    [Fact]
    public void Presence_PingYoq_CheckIndanBeri3xIntervalOtgan_SignalLost_AksHoldaInside()
    {
        var lost = LocationTracking.Evaluate(Input([], nowMinute: 16));
        lost.State.Should().Be(PresenceState.SignalLost);
        lost.LastPingAt.Should().BeNull();

        var fresh = LocationTracking.Evaluate(Input([], nowMinute: 3));
        fresh.State.Should().Be(PresenceState.Inside, "qabul qilingan check-in radius ichida bo'lgan");
        fresh.DistanceM.Should().Be(25, "ping bo'lmasa — check-in masofasi");
    }

    [Fact]
    public void Presence_CheckOutYokiAvtoYopilish_Stopped_RoziliksizdanUstun()
    {
        LocationTracking.Evaluate(Input([P(5, LocationPingStatus.Outside)], 400, consent: false, checkOut: T0.AddMinutes(300)))
            .State.Should().Be(PresenceState.Stopped);
        LocationTracking.Evaluate(Input([P(5, LocationPingStatus.Inside)], nowMinute: 601))
            .State.Should().Be(PresenceState.Stopped, "avtomatik yopilish vaqti o'tgan");
    }

    [Fact]
    public void Presence_RozilikYoq_NoConsent()
        => LocationTracking.Evaluate(Input([], nowMinute: 60, consent: false)).State.Should().Be(PresenceState.NoConsent);

    [Fact]
    public void AcceptWindow_24SoatVa2Daqiqa()
    {
        var now = T0;
        LocationTracking.IsWithinAcceptWindow(now.AddHours(-24), now).Should().BeTrue();
        LocationTracking.IsWithinAcceptWindow(now.AddHours(-24).AddSeconds(-1), now).Should().BeFalse();
        LocationTracking.IsWithinAcceptWindow(now.AddMinutes(2), now).Should().BeTrue();
        LocationTracking.IsWithinAcceptWindow(now.AddMinutes(2).AddSeconds(1), now).Should().BeFalse();
    }

    [Fact]
    public void NormalizeInstant_UtcVaMillisekund()
    {
        var local = new DateTimeOffset(2026, 10, 14, 9, 5, 0, TimeSpan.FromHours(5)).AddTicks(12_345);

        var normalized = LocationTracking.NormalizeInstant(local);

        normalized.Offset.Should().Be(TimeSpan.Zero);
        normalized.Should().Be(new DateTimeOffset(2026, 10, 14, 4, 5, 0, 1, TimeSpan.Zero));
    }

    [Fact]
    public void Ping_Record_HolatniHisoblaydiVaVaqtniNormallashtiradi()
    {
        var ping = LocationPing.Record(
            Guid.CreateVersion7(), Guid.CreateVersion7(), new DateOnly(2026, 10, 14), T0.AddTicks(5),
            new GeoPoint(41.3, 69.2), accuracyM: 12, distanceM: 400, radiusM: 150, minAccuracyM: 100,
            isMocked: true, LocationPingSource.Foreground, T0);

        ping.Status.Should().Be(LocationPingStatus.Outside);
        ping.OccurredAt.Should().Be(T0);
        ping.IsMocked.Should().BeTrue();
        ping.Source.Should().Be(LocationPingSource.Foreground);
        ping.Should().BeAssignableTo<Amaliyotchi.Domain.Common.IAuditExempt>("har ping audit'ga yozilsa jadval portlaydi");

        FluentActions.Invoking(() => LocationPing.Record(
                Guid.Empty, Guid.CreateVersion7(), new DateOnly(2026, 10, 14), T0, new GeoPoint(0, 0), 1, 1, 150, 100, false,
                LocationPingSource.Background, T0))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void Rozilik_BeriladiTakrorVaqtSaqlanadi_QaytaribOlinadi()
    {
        var profile = StudentProfile.Create(Guid.CreateVersion7(), "341030", Guid.CreateVersion7());
        profile.HasTrackingConsent.Should().BeFalse();

        profile.GiveTrackingConsent(T0);
        profile.GiveTrackingConsent(T0.AddHours(1));
        profile.TrackingConsentAt.Should().Be(T0);

        profile.WithdrawTrackingConsent();
        profile.HasTrackingConsent.Should().BeFalse();
    }

    [Theory]
    [InlineData(SettingKeys.TrackingEnabled, "true", null)]
    [InlineData(SettingKeys.TrackingIntervalSec, "300", "sec")]
    [InlineData(SettingKeys.TrackingDistanceFilterM, "50", "m")]
    public void Sozlamalar_StandartQiymatVaBirlik(string key, string defaultValue, string? unit)
    {
        var definition = SettingKeys.Get(key);
        definition.DefaultValue.Should().Be(defaultValue);
        definition.Unit.Should().Be(unit);
    }

    [Theory]
    [InlineData(SettingKeys.TrackingIntervalSec, "59")]
    [InlineData(SettingKeys.TrackingIntervalSec, "901")]
    [InlineData(SettingKeys.TrackingDistanceFilterM, "9")]
    [InlineData(SettingKeys.TrackingDistanceFilterM, "501")]
    [InlineData(SettingKeys.TrackingEnabled, "balki")]
    public void Sozlamalar_OraliqdanTashqari_Xato(string key, string value)
        => FluentActions.Invoking(() => SettingKeys.Get(key).Validate(value)).Should().Throw<DomainException>();

    [Theory]
    [InlineData(SettingKeys.TrackingIntervalSec, "60")]
    [InlineData(SettingKeys.TrackingIntervalSec, "900")]
    [InlineData(SettingKeys.TrackingDistanceFilterM, "10")]
    [InlineData(SettingKeys.TrackingDistanceFilterM, "500")]
    public void Sozlamalar_Chegaralar_Qabul(string key, string value)
        => SettingKeys.Get(key).Validate(value).Should().Be(value);
}

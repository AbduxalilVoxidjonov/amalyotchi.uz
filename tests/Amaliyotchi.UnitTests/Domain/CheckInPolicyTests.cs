using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

/// <summary>M08 DoD holatlari: qoidalar 09:00–09:15 keldi, 09:15–10:30 kech, 10:30 dan keyin yopiq;
/// check-out 17:00–18:00; radius va GPS aniqligi.</summary>
public sealed class CheckInPolicyTests
{
    private static readonly CheckInRules Rules = CheckInRules.Default;
    private static readonly DateOnly Monday = new(2026, 9, 14);
    private static readonly DateOnly Sunday = new(2026, 9, 13);

    private static CheckInContext Ok(TimeOnly? now = null) => new(
        Date: Monday,
        LocalNow: now ?? new TimeOnly(9, 5),
        ApplicationApproved: true,
        PeriodStarted: true,
        PeriodEnded: false,
        WorkDays: WorkDays.MondayToSaturday,
        IsHoliday: false,
        HasApprovedLeave: false,
        AlreadyCheckedIn: false,
        AccuracyM: 20,
        DistanceM: 45,
        RadiusM: 200);

    [Fact]
    public void Oynada_RadiusIchida_KeldiDebQabulQilinadi()
    {
        var verdict = CheckInPolicy.Evaluate(Ok(new TimeOnly(9, 2)), Rules);

        verdict.Accepted.Should().BeTrue();
        verdict.IsLate.Should().BeFalse();
        verdict.Status.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public void OynadanOldin_RadEtiladi()
    {
        var verdict = CheckInPolicy.Evaluate(Ok(new TimeOnly(8, 45)), Rules);

        verdict.Accepted.Should().BeFalse();
        verdict.Reason.Should().Be(CheckInRejectReason.WindowNotOpen);
    }

    [Theory]
    [InlineData(9, 15)]
    [InlineData(9, 41)]
    [InlineData(10, 29)]
    public void KechikishChegarasidanKeyin_KechKeldi(int hour, int minute)
    {
        var verdict = CheckInPolicy.Evaluate(Ok(new TimeOnly(hour, minute)), Rules);

        verdict.Accepted.Should().BeTrue();
        verdict.IsLate.Should().BeTrue();
        verdict.Status.Should().Be(AttendanceStatus.Late);
    }

    [Fact]
    public void OynaYopilgach_RadEtiladi()
    {
        var verdict = CheckInPolicy.Evaluate(Ok(new TimeOnly(10, 30)), Rules);

        verdict.Accepted.Should().BeFalse();
        verdict.Reason.Should().Be(CheckInRejectReason.WindowClosed);
    }

    [Theory]
    [InlineData(200, true)]   // aynan chegarada — ichida
    [InlineData(195, true)]   // chegaradan 5 m ichkarida
    [InlineData(205, false)]  // chegaradan 5 m tashqarida
    [InlineData(1200, false)]
    public void RadiusChegarasi_TalabaFoydasigaTeng(double distance, bool expected)
    {
        var verdict = CheckInPolicy.Evaluate(Ok() with { DistanceM = distance }, Rules);

        verdict.Accepted.Should().Be(expected);
        if (!expected)
            verdict.Reason.Should().Be(CheckInRejectReason.OutOfRadius);
    }

    [Fact]
    public void GpsAniqligiYomon_RadEtiladi()
    {
        var verdict = CheckInPolicy.Evaluate(Ok() with { AccuracyM = 150 }, Rules);

        verdict.Accepted.Should().BeFalse();
        verdict.Reason.Should().Be(CheckInRejectReason.PoorAccuracy);
    }

    [Fact]
    public void GpsAniqligi_ChegaragaTeng_QabulQilinadi()
    {
        var verdict = CheckInPolicy.Evaluate(Ok() with { AccuracyM = 100 }, Rules);

        verdict.Accepted.Should().BeTrue();
    }

    [Fact]
    public void IkkiMartaCheckIn_RadEtiladi()
    {
        var verdict = CheckInPolicy.Evaluate(Ok() with { AlreadyCheckedIn = true }, Rules);

        verdict.Reason.Should().Be(CheckInRejectReason.AlreadyCheckedIn);
    }

    [Fact]
    public void DamOlishKuni_RadEtiladi()
    {
        var verdict = CheckInPolicy.Evaluate(Ok() with { Date = Sunday }, Rules);

        verdict.Reason.Should().Be(CheckInRejectReason.NotWorkDay);
    }

    [Fact]
    public void Bayram_RadEtiladi()
    {
        var verdict = CheckInPolicy.Evaluate(Ok() with { IsHoliday = true }, Rules);

        verdict.Reason.Should().Be(CheckInRejectReason.NotWorkDay);
    }

    [Fact]
    public void RuxsatliKun_RadEtiladi()
    {
        var verdict = CheckInPolicy.Evaluate(Ok() with { HasApprovedLeave = true }, Rules);

        verdict.Reason.Should().Be(CheckInRejectReason.OnLeave);
    }

    [Fact]
    public void ArizaTasdiqlanmagan_RadEtiladi()
    {
        var verdict = CheckInPolicy.Evaluate(Ok() with { ApplicationApproved = false }, Rules);

        verdict.Reason.Should().Be(CheckInRejectReason.NotApproved);
    }

    [Fact]
    public void DavrBoshlanmagan_RadEtiladi()
    {
        var verdict = CheckInPolicy.Evaluate(Ok() with { PeriodStarted = false }, Rules);

        verdict.Reason.Should().Be(CheckInRejectReason.PeriodNotStarted);
    }

    [Fact]
    public void DavrTugagan_RadEtiladi()
    {
        var verdict = CheckInPolicy.Evaluate(Ok() with { PeriodEnded = true }, Rules);

        verdict.Reason.Should().Be(CheckInRejectReason.PeriodEnded);
    }

    [Fact]
    public void RadSabablari_UstuvorTartibda()
    {
        // Ariza tasdiqlanmagan + radius tashqarisi → avval ariza sababi
        var verdict = CheckInPolicy.Evaluate(Ok() with { ApplicationApproved = false, DistanceM = 5000 }, Rules);

        verdict.Reason.Should().Be(CheckInRejectReason.NotApproved);
    }

    // ---- Check-out ----

    private static CheckOutContext OutOk(TimeOnly? now = null) => new(
        LocalNow: now ?? new TimeOnly(17, 5),
        HasCheckedIn: true,
        AlreadyCheckedOut: false,
        AutoClosed: false,
        AccuracyM: 20,
        DistanceM: 45,
        RadiusM: 200);

    [Fact]
    public void CheckOut_IshTugagach_QabulQilinadi()
    {
        var verdict = CheckInPolicy.EvaluateCheckOut(OutOk(new TimeOnly(17, 0)), Rules);

        verdict.Accepted.Should().BeTrue();
    }

    [Fact]
    public void CheckOut_Erta_RadEtiladi()
    {
        var verdict = CheckInPolicy.EvaluateCheckOut(OutOk(new TimeOnly(16, 30)), Rules);

        verdict.Reason.Should().Be(CheckInRejectReason.WindowNotOpen);
    }

    [Fact]
    public void CheckOut_AvtoYopishdanKeyin_RadEtiladi()
    {
        var verdict = CheckInPolicy.EvaluateCheckOut(OutOk(new TimeOnly(18, 0)), Rules);

        verdict.Reason.Should().Be(CheckInRejectReason.WindowClosed);
    }

    [Fact]
    public void CheckOut_CheckInsiz_RadEtiladi()
    {
        var verdict = CheckInPolicy.EvaluateCheckOut(OutOk() with { HasCheckedIn = false }, Rules);

        verdict.Reason.Should().Be(CheckInRejectReason.NoCheckIn);
    }

    [Fact]
    public void CheckOut_Takroriy_RadEtiladi()
    {
        var verdict = CheckInPolicy.EvaluateCheckOut(OutOk() with { AlreadyCheckedOut = true }, Rules);

        verdict.Reason.Should().Be(CheckInRejectReason.AlreadyCheckedOut);
    }

    [Fact]
    public void CheckOut_RadiusTashqarisi_RadEtiladi()
    {
        var verdict = CheckInPolicy.EvaluateCheckOut(OutOk() with { DistanceM = 900 }, Rules);

        verdict.Reason.Should().Be(CheckInRejectReason.OutOfRadius);
    }

    // ---- Qoidalar va istisnolar ----

    [Fact]
    public void Qoidalar_HisoblanganVaqtlar()
    {
        Rules.LateAfter.Should().Be(new TimeOnly(9, 15));
        Rules.WindowEnd.Should().Be(new TimeOnly(10, 30));
        Rules.CheckOutFrom.Should().Be(new TimeOnly(17, 0));
        Rules.AutoCloseAt.Should().Be(new TimeOnly(18, 0));
    }

    [Fact]
    public void Qoidalar_QisqaKun_OynaVaKechikishIshTugashigachaQisqaradi()
    {
        var rules = new CheckInRules(new TimeOnly(9, 0), new TimeOnly(10, 0), 15, 90, 60, 100);

        rules.CheckInWindowMinutes.Should().Be(90);
        rules.EffectiveWindowMinutes.Should().Be(60);
        rules.WindowEnd.Should().Be(new TimeOnly(10, 0));
        rules.LateAfter.Should().Be(new TimeOnly(9, 15));

        // Kechikish chegarasi ham oynadan oshmaydi: 30 daqiqalik kun, 45 daqiqa kechikish.
        var tiny = new CheckInRules(new TimeOnly(9, 0), new TimeOnly(9, 30), 45, 90, 60, 100);
        tiny.WindowEnd.Should().Be(new TimeOnly(9, 30));
        tiny.LateAfter.Should().Be(new TimeOnly(9, 30));
    }

    [Fact]
    public void Qoidalar_AvtoYopishYarimTundanOtmaydi()
    {
        var rules = new CheckInRules(new TimeOnly(20, 0), new TimeOnly(23, 30), 15, 90, 60, 100);

        rules.AutoCloseAt.Should().Be(TimeOnly.MaxValue);
        rules.WindowEnd.Should().Be(new TimeOnly(21, 30));
    }

    [Fact]
    public void Qoidalar_WithHours_DaqiqalarSaqlanadi_VaqtlarYangi()
    {
        var own = Rules.WithHours(new TimeOnly(13, 0), new TimeOnly(18, 0));

        own.LateToleranceMinutes.Should().Be(Rules.LateToleranceMinutes);
        own.CheckInWindowMinutes.Should().Be(Rules.CheckInWindowMinutes);
        own.MinAccuracyM.Should().Be(Rules.MinAccuracyM);
        own.LateAfter.Should().Be(new TimeOnly(13, 15));
        own.WindowEnd.Should().Be(new TimeOnly(14, 30));
        own.AutoCloseAt.Should().Be(new TimeOnly(19, 0));
    }

    [Fact]
    public void Qoidalar_OynaKechikishdanKichik_XatoBeradi()
    {
        var act = () => new CheckInRules(new TimeOnly(9, 0), new TimeOnly(17, 0), 30, 30, 60, 100);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(CheckInRejectReason.OutOfRadius, typeof(ConflictException))]
    [InlineData(CheckInRejectReason.AlreadyCheckedIn, typeof(ConflictException))]
    [InlineData(CheckInRejectReason.NoCheckIn, typeof(ConflictException))]
    [InlineData(CheckInRejectReason.WindowClosed, typeof(DomainException))]
    [InlineData(CheckInRejectReason.PoorAccuracy, typeof(DomainException))]
    public void RadSababi_ToGriIstisnogaAylanadi(CheckInRejectReason reason, Type expected)
    {
        reason.ToException().Should().BeOfType(expected);
    }

    [Fact]
    public void RadEtilganVerdict_DavomatgaYozilmaydi()
    {
        var verdict = CheckInVerdict.Reject(CheckInRejectReason.OutOfRadius);

        var act = () => DailyAttendance.CheckIn(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Monday, DateTimeOffset.UtcNow, 900, 20, verdict);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Davomat_CheckInVaCheckOut_ToliqKun()
    {
        var now = DateTimeOffset.UtcNow;
        var attendance = DailyAttendance.CheckIn(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Monday, now, 45, 20, CheckInVerdict.Accept(isLate: true));

        attendance.Status.Should().Be(AttendanceStatus.Late);
        attendance.CheckOut(now.AddHours(8), 30);
        attendance.HasCheckedOut.Should().BeTrue();

        var again = () => attendance.CheckOut(now.AddHours(9), 30);
        again.Should().Throw<ConflictException>();
    }

    [Fact]
    public void Davomat_QoldaTuzatish_SababsizBolmaydi()
    {
        var attendance = DailyAttendance.Excuse(Guid.CreateVersion7(), Guid.CreateVersion7(), Monday, Guid.CreateVersion7());

        var act = () => attendance.ManualFix(AttendanceStatus.Present, Guid.CreateVersion7(), "  ", DateTimeOffset.UtcNow);

        act.Should().Throw<DomainException>();
        attendance.Status.Should().Be(AttendanceStatus.Excused);
    }

    [Fact]
    public void Davomat_AutoClose_FaqatCheckInBorKunga()
    {
        var now = DateTimeOffset.UtcNow;
        var excused = DailyAttendance.Excuse(Guid.CreateVersion7(), Guid.CreateVersion7(), Monday, Guid.CreateVersion7());
        var present = DailyAttendance.CheckIn(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Monday, now, 45, 20, CheckInVerdict.Accept());

        excused.AutoClose(now);
        present.AutoClose(now.AddHours(9));

        excused.AutoClosed.Should().BeFalse();
        present.AutoClosed.Should().BeTrue();
        present.CheckOutAt.Should().Be(now.AddHours(9));
    }
}

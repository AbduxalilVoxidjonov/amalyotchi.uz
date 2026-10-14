using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Practice;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

/// <summary>Kun bo'yicha kundalik yozish qoidasi (<see cref="DiaryDayPolicy"/>): kelajak, dam olish/bayram, davr tashqarisi,
/// yopiq davr, mavjud yozuv / rewrite.</summary>
public sealed class DiaryDayPolicyTests
{
    // 2026-10-14 — chorshanba. Davr: 2026-10-01 (pay) .. 2026-10-31, Du–Ju.
    private static readonly DateOnly Today = new(2026, 10, 14);
    private static readonly DateOnly Monday = new(2026, 10, 12);
    private static readonly DateOnly Saturday = new(2026, 10, 10);
    private static readonly DateOnly Holiday = new(2026, 10, 9);

    private static PracticePeriod NewPeriod(DateOnly start, DateOnly end, bool closed = false)
    {
        var period = PracticePeriod.Create(
            "Kuzgi", Guid.CreateVersion7(), start, end, Guid.CreateVersion7(),
            CheckInRules.Default, WorkDays.MondayToFriday, 10, dailyReportRequired: true);
        period.Activate();
        if (closed)
            period.Close();
        return period;
    }

    private static readonly PracticePeriod Autumn = NewPeriod(new(2026, 10, 1), new(2026, 10, 31));

    private static bool IsHoliday(DateOnly d) => d == Holiday;

    private static DiaryDayDecision Evaluate(
        IReadOnlyCollection<PracticePeriod> periods, DateOnly date, DiaryStatus? status = null, bool requireWorkDay = true)
        => DiaryDayPolicy.Evaluate(periods, IsHoliday, Today, date, status, requireWorkDay);

    [Fact]
    public void OtganIshKuni_YozuvYoq_Mumkin_DavrQaytadi()
    {
        var decision = Evaluate([Autumn], Monday);

        decision.CanWrite.Should().BeTrue();
        decision.Period.Should().BeSameAs(Autumn);
        decision.Reason.Should().BeNull();
    }

    [Fact]
    public void Bugun_IshKuni_Mumkin()
        => Evaluate([Autumn], Today).CanWrite.Should().BeTrue();

    [Fact]
    public void Kelajak_RadEtiladi()
    {
        var decision = Evaluate([Autumn], Today.AddDays(1));

        decision.Block.Should().Be(DiaryDayBlock.Future);
        decision.Reason.Should().Be("Kelajakdagi kun uchun hisobot yuborib bo'lmaydi.");
    }

    [Fact]
    public void DamOlishKuni_RadEtiladi()
    {
        var decision = Evaluate([Autumn], Saturday);

        decision.Block.Should().Be(DiaryDayBlock.NotWorkDay);
        decision.Reason.Should().Be("Dam olish kuni uchun hisobot yuborilmaydi.");
    }

    [Fact]
    public void Bayram_RadEtiladi()
        => Evaluate([Autumn], Holiday).Block.Should().Be(DiaryDayBlock.NotWorkDay);

    [Fact]
    public void EskiBugunYoli_IshKuniTekshirilmaydi()
    {
        // requireWorkDay = false (canWriteDiary / date'siz POST) — shanba ham ochiq davr ichida bo'lsa mumkin.
        var saturdayToday = DiaryDayPolicy.Evaluate([Autumn], IsHoliday, Saturday, Saturday, null, requireWorkDay: false);
        saturdayToday.CanWrite.Should().BeTrue();
    }

    [Fact]
    public void DavrdanOldin_HaliBoshlanmagan()
    {
        var decision = Evaluate([Autumn], new DateOnly(2026, 9, 28));

        decision.Block.Should().Be(DiaryDayBlock.Period);
        decision.Reason.Should().Be(DiaryDayPolicy.PeriodNotStartedMessage);
    }

    [Fact]
    public void TugaganDavrdanKeyin_Yakunlangan()
    {
        var ended = NewPeriod(new(2026, 9, 1), new(2026, 9, 30));

        var decision = Evaluate([ended], new DateOnly(2026, 10, 5));

        decision.Block.Should().Be(DiaryDayBlock.Period);
        decision.Reason.Should().Be(DiaryDayPolicy.PeriodEndedMessage);
    }

    [Fact]
    public void YopilganDavr_IchidagiOtganKun_Yakunlangan()
    {
        var closed = NewPeriod(new(2026, 10, 1), new(2026, 10, 31), closed: true);

        var decision = Evaluate([closed], Monday);

        decision.Block.Should().Be(DiaryDayBlock.Period);
        decision.Reason.Should().Be(DiaryDayPolicy.PeriodEndedMessage);
    }

    [Fact]
    public void DavrYoq_FaolDavrYoq()
        => Evaluate([], Monday).Reason.Should().Be(DiaryDayPolicy.NoPeriodMessage);

    [Theory]
    [InlineData(DiaryStatus.Submitted)]
    [InlineData(DiaryStatus.Seen)]
    [InlineData(DiaryStatus.Approved)]
    public void YozuvBor_OtganKun_AllaqachonYuborilgan(DiaryStatus status)
    {
        var decision = Evaluate([Autumn], Monday, status);

        decision.Block.Should().Be(DiaryDayBlock.AlreadySubmitted);
        decision.Reason.Should().Be("Bu kun uchun hisobot allaqachon yuborilgan.");
    }

    [Fact]
    public void YozuvBor_Bugun_EskiMatn()
        => Evaluate([Autumn], Today, DiaryStatus.Submitted).Reason.Should().Be("Bugungi hisobot allaqachon yuborilgan.");

    [Fact]
    public void Rewrite_QaytaYuborishMumkin()
    {
        var decision = Evaluate([Autumn], Monday, DiaryStatus.Rewrite);

        decision.CanWrite.Should().BeTrue();
        decision.Period.Should().BeSameAs(Autumn);
    }

    [Fact]
    public void Rewrite_YopilganDavrda_Taqiqlanadi()
    {
        var closed = NewPeriod(new(2026, 10, 1), new(2026, 10, 31), closed: true);

        Evaluate([closed], Monday, DiaryStatus.Rewrite).Block.Should().Be(DiaryDayBlock.Period);
    }

    [Fact]
    public void Kelajak_DavrQoidasidanUstun()
        => Evaluate([], Today.AddDays(3)).Block.Should().Be(DiaryDayBlock.Future);

    [Fact]
    public void KechYuborilgan_ToshkentSanasiBoyicha()
    {
        // 2026-10-12 19:30 UTC = 2026-10-13 00:30 Toshkent → kech.
        DiaryDayPolicy.IsLate(Monday, new DateTimeOffset(2026, 10, 12, 19, 30, 0, TimeSpan.Zero)).Should().BeTrue();
        // 2026-10-12 18:30 UTC = 2026-10-12 23:30 Toshkent → o'z vaqtida.
        DiaryDayPolicy.IsLate(Monday, new DateTimeOffset(2026, 10, 12, 18, 30, 0, TimeSpan.Zero)).Should().BeFalse();
    }
}

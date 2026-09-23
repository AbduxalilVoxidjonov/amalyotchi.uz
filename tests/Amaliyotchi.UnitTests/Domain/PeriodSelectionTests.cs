using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Practice;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

/// <summary>Yagona davr tanlash qoidasi (<see cref="PeriodSelection"/>): bir guruhda kuzgi va bahorgi davr.</summary>
public sealed class PeriodSelectionTests
{
    private static readonly PracticePeriod Autumn = NewPeriod("Kuzgi", new(2026, 9, 1), new(2026, 10, 31));
    private static readonly PracticePeriod Spring = NewPeriod("Bahorgi", new(2027, 2, 1), new(2027, 3, 15));
    private static readonly PracticePeriod[] Both = [Spring, Autumn];

    private static readonly DateOnly InAutumn = new(2026, 10, 1);
    private static readonly DateOnly Between = new(2026, 11, 20);
    private static readonly DateOnly InSpring = new(2027, 2, 10);
    private static readonly DateOnly BeforeAll = new(2026, 8, 1);
    private static readonly DateOnly AfterAll = new(2027, 5, 1);

    private static PracticePeriod NewPeriod(string name, DateOnly start, DateOnly end, bool closed = false)
    {
        var period = PracticePeriod.Create(
            name, Guid.CreateVersion7(), start, end, Guid.CreateVersion7(),
            CheckInRules.Default, WorkDays.MondayToFriday, 10, dailyReportRequired: true);
        period.Activate();
        if (closed)
            period.Close();
        return period;
    }

    [Fact]
    public void DavrIchida_HammaMaqsadlar_DavomEtayotganni_Tanlaydi()
    {
        foreach (var purpose in Enum.GetValues<PeriodPurpose>())
            PeriodSelection.Select(Both, InAutumn, purpose).Should().BeSameAs(Autumn, purpose.ToString());
    }

    [Fact]
    public void Oraliqda_Sukut_OxirgiTugagan_Ariza_Kelgusi_Checkin_Yoq()
    {
        PeriodSelection.Select(Both, Between, PeriodPurpose.Default).Should().BeSameAs(Autumn);
        PeriodSelection.Select(Both, Between, PeriodPurpose.Enrollment).Should().BeSameAs(Spring);
        PeriodSelection.Select(Both, Between, PeriodPurpose.Current).Should().BeSameAs(Spring);
        PeriodSelection.Select(Both, Between, PeriodPurpose.Ongoing).Should().BeNull();
    }

    [Fact]
    public void HammasidanOldin_Sukut_EngYaqinKelgusi()
    {
        PeriodSelection.Select(Both, BeforeAll, PeriodPurpose.Default).Should().BeSameAs(Autumn);
        PeriodSelection.Select(Both, BeforeAll, PeriodPurpose.Ongoing).Should().BeNull();
    }

    [Fact]
    public void HammasidanKeyin_Sukut_EngSonggiTugagan_ArizaYoq()
    {
        PeriodSelection.Select(Both, AfterAll, PeriodPurpose.Default).Should().BeSameAs(Spring);
        PeriodSelection.Select(Both, AfterAll, PeriodPurpose.Current).Should().BeSameAs(Spring);
        PeriodSelection.Select(Both, AfterAll, PeriodPurpose.Enrollment).Should().BeNull();
    }

    [Fact]
    public void YopilganDavr_TarixdaQoladi_LekinDavomEtayotganVaKelgusiEmas()
    {
        var closedAutumn = NewPeriod("Kuzgi (yopilgan)", new(2026, 9, 1), new(2026, 10, 31), closed: true);
        var closedSpring = NewPeriod("Bahorgi (yopilgan)", new(2027, 2, 1), new(2027, 3, 15), closed: true);

        // Muddatidan oldin yopilgan davr (bugunni o'z ichiga oladi) — davom etmaydi, lekin statistika uchun "tugagan".
        PeriodSelection.Select([closedAutumn], InAutumn, PeriodPurpose.Ongoing).Should().BeNull();
        PeriodSelection.Select([closedAutumn], InAutumn, PeriodPurpose.Default).Should().BeSameAs(closedAutumn);
        PeriodSelection.Select([closedAutumn, Spring], Between, PeriodPurpose.Default).Should().BeSameAs(closedAutumn);

        // Yopilgan kelgusi davrga ariza berilmaydi.
        PeriodSelection.Select([Autumn, closedSpring], Between, PeriodPurpose.Enrollment).Should().BeNull();
        PeriodSelection.Select([Autumn, closedSpring], Between, PeriodPurpose.Default).Should().BeSameAs(Autumn);
    }

    [Fact]
    public void Chegaralar_BoshlanishVaTugashKuni_DavrIchida()
    {
        PeriodSelection.Select(Both, Autumn.StartDate, PeriodPurpose.Ongoing).Should().BeSameAs(Autumn);
        PeriodSelection.Select(Both, Autumn.EndDate, PeriodPurpose.Ongoing).Should().BeSameAs(Autumn);
        PeriodSelection.Select(Both, Autumn.EndDate.AddDays(1), PeriodPurpose.Ongoing).Should().BeNull();
        PeriodSelection.Select(Both, Autumn.EndDate.AddDays(1), PeriodPurpose.Default).Should().BeSameAs(Autumn);
    }

    [Fact]
    public void BoshRoyxat_Null()
    {
        PeriodSelection.Select(Array.Empty<PracticePeriod>(), InAutumn, PeriodPurpose.Default).Should().BeNull();
    }
}

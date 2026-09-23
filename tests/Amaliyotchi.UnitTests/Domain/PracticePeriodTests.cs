using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

/// <summary>Admin davr boshqaruvi qoidalari: hisoblangan holat, sanalarni o'zgartirish, guruhni ajratish, yopish, o'chirish.</summary>
public sealed class PracticePeriodTests
{
    private static readonly DateOnly Today = new(2026, 9, 23);

    private static PracticePeriod NewPeriod(DateOnly start, DateOnly end, string name = "Kuzgi amaliyot")
        => PracticePeriod.Create(
            name, Guid.CreateVersion7(), start, end, Guid.CreateVersion7(),
            CheckInRules.Default, WorkDays.MondayToFriday, 10, dailyReportRequired: true);

    [Fact]
    public void Holat_BoshlanishgachaPlanned_BoshlangachActive_YopilgachClosed()
    {
        var period = NewPeriod(Today.AddDays(1), Today.AddDays(30));
        period.Activate();

        period.EffectiveStatus(Today).Should().Be(PracticePeriodStatus.Planned);
        period.EffectiveStatus(Today.AddDays(1)).Should().Be(PracticePeriodStatus.Active);
        period.EffectiveStatus(Today.AddDays(60)).Should().Be(PracticePeriodStatus.Active); // tugagan, lekin yopilmagan

        period.Close();
        period.EffectiveStatus(Today).Should().Be(PracticePeriodStatus.Closed);
        period.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Yopish_IkkinchiMarta_409()
    {
        var period = NewPeriod(Today, Today.AddDays(10));
        period.Close();

        var again = () => period.Close();

        again.Should().Throw<ConflictException>();
    }

    [Fact]
    public void Reschedule_Rejalashtirilgan_IkkalaSanaOzgaradi()
    {
        var period = NewPeriod(Today.AddDays(5), Today.AddDays(30));

        period.Reschedule(Today.AddDays(7), Today.AddDays(40), 24, Today);

        period.StartDate.Should().Be(Today.AddDays(7));
        period.EndDate.Should().Be(Today.AddDays(40));
        period.RequiredDays.Should().Be(24);
    }

    [Fact]
    public void Reschedule_FaolDavrdaBoshlanishOzgarsa_400()
    {
        var period = NewPeriod(Today.AddDays(-5), Today.AddDays(30));

        var act = () => period.Reschedule(Today.AddDays(-4), Today.AddDays(30), 20, Today);

        act.Should().Throw<DomainException>().WithMessage("*boshlanish sanasini*")
            .Which.Should().BeOfType<DomainException>("400, 409 emas");
    }

    [Fact]
    public void Reschedule_FaolDavrda_TugashUzayadiQisqaradi_BugundanOldingaEmas()
    {
        var period = NewPeriod(Today.AddDays(-5), Today.AddDays(30));

        period.Reschedule(period.StartDate, Today.AddDays(40), 30, Today);
        period.EndDate.Should().Be(Today.AddDays(40));

        period.Reschedule(period.StartDate, Today, 5, Today);
        period.EndDate.Should().Be(Today);

        var past = () => period.Reschedule(period.StartDate, Today.AddDays(-1), 4, Today);
        past.Should().Throw<DomainException>().WithMessage("*bugundan oldin*");
    }

    [Fact]
    public void Reschedule_TugashBoshlanishdanOldin_400()
    {
        var period = NewPeriod(Today.AddDays(5), Today.AddDays(30));

        var act = () => period.Reschedule(Today.AddDays(10), Today.AddDays(9), 0, Today);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void YopilganDavr_TahrirVaGuruhOzgarishi_409()
    {
        var period = NewPeriod(Today.AddDays(-5), Today.AddDays(30));
        var group = Guid.CreateVersion7();
        period.AttachGroup(group);
        period.Close();

        ((Action)(() => period.Reschedule(period.StartDate, Today.AddDays(40), 30, Today))).Should().Throw<ConflictException>();
        ((Action)(() => period.Rename("Yangi nom"))).Should().Throw<ConflictException>();
        ((Action)(() => period.DetachGroup(group, hasAttendanceRecords: false))).Should().Throw<ConflictException>();
        ((Action)(() => period.AttachGroup(Guid.CreateVersion7()))).Should().Throw<ConflictException>();
    }

    [Fact]
    public void GuruhniAjratish_DavomatBolsa409_BolmasaAjraladi()
    {
        var period = NewPeriod(Today.AddDays(-5), Today.AddDays(30));
        var withAttendance = Guid.CreateVersion7();
        var clean = Guid.CreateVersion7();
        period.AttachGroup(withAttendance);
        period.AttachGroup(clean);

        var blocked = () => period.DetachGroup(withAttendance, hasAttendanceRecords: true);
        blocked.Should().Throw<ConflictException>().WithMessage("*davomat yozuvlari bor*");

        period.DetachGroup(clean, hasAttendanceRecords: false);
        period.Groups.Should().ContainSingle(g => g.StudentGroupId == withAttendance);
    }

    [Fact]
    public void Ochirish_DavomatBolsa409_BolmasaSoftDelete()
    {
        var period = NewPeriod(Today, Today.AddDays(10));
        var now = DateTimeOffset.UtcNow;

        var blocked = () => period.Delete(now, hasAttendanceRecords: true);
        blocked.Should().Throw<ConflictException>().WithMessage("*Yopish*");
        period.IsDeleted.Should().BeFalse();

        period.Delete(now, hasAttendanceRecords: false);
        period.IsDeleted.Should().BeTrue();
        period.DeletedAt.Should().Be(now);
    }

    [Fact]
    public void Nom_TrimVaUzunlikTekshiriladi()
    {
        var period = NewPeriod(Today, Today.AddDays(10), "  Bahorgi  ");
        period.Name.Should().Be("Bahorgi");

        var tooLong = () => period.Rename(new string('a', PracticePeriod.NameMaxLength + 1));
        tooLong.Should().Throw<DomainException>();
    }

    [Fact]
    public void IshKunlari_HaftaKuniVaBayramBoyichaSanaladi()
    {
        // 2026-09-21 (Du) .. 2026-10-04 (Ya): 2 hafta, Du–Ju = 10 kun; 2026-10-01 bayram → 9.
        var start = new DateOnly(2026, 9, 21);
        var end = new DateOnly(2026, 10, 4);
        var holiday = new DateOnly(2026, 10, 1);

        PracticePeriod.CountWorkDays(start, end, WorkDays.MondayToFriday, _ => false).Should().Be(10);
        PracticePeriod.CountWorkDays(start, end, WorkDays.MondayToFriday, d => d == holiday).Should().Be(9);
        PracticePeriod.CountWorkDays(start, end, WorkDays.MondayToSaturday, _ => false).Should().Be(12);
    }
}

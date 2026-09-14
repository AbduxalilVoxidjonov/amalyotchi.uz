using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

public sealed class SettingsAndPeriodTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Theory]
    [InlineData(SettingKeys.GeofenceRadius, " 250 ", "250")]
    [InlineData(SettingKeys.DailyReportRequired, "1", "true")]
    [InlineData(SettingKeys.WorkDays, "6, 1,2,3,4,5", "1,2,3,4,5,6")]
    public void Sozlama_KalitBoyichaNormallashtiriladi(string key, string value, string expected)
    {
        AppSetting.Create(key, value, Now).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(SettingKeys.GeofenceRadius, "20")]
    [InlineData(SettingKeys.GeofenceRadius, "abc")]
    [InlineData(SettingKeys.DailyReportRequired, "maybe")]
    [InlineData(SettingKeys.WorkDays, "0,8")]
    [InlineData("unknownKey", "1")]
    public void Sozlama_NotogriQiymat_XatoBeradi(string key, string value)
    {
        var act = () => AppSetting.Create(key, value, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Sozlama_StandartQiymatlar_ValidatsiyadanOtadi()
    {
        foreach (var definition in SettingKeys.All)
            AppSetting.CreateDefault(definition.Key, Now).Value.Should().Be(definition.DefaultValue);

        AppSetting.CreateDefault(SettingKeys.WorkDays, Now).AsWorkDays().Should().Be(WorkDays.MondayToSaturday);
        AppSetting.CreateDefault(SettingKeys.CheckInWindow, Now).AsInt().Should().Be(90);
        AppSetting.CreateDefault(SettingKeys.DailyReportRequired, Now).AsBool().Should().BeTrue();
    }

    [Fact]
    public void Sozlama_Update_OzgarmasaFalse()
    {
        var setting = AppSetting.CreateDefault(SettingKeys.LateTolerance, Now);

        setting.Update("15", Now, null).Should().BeFalse();
        setting.Update("20", Now, null).Should().BeTrue();
        setting.AsInt().Should().Be(20);
    }

    [Fact]
    public void Bayram_TakrorlanuvchiHarYilgaTegishli()
    {
        var navruz = Holiday.Create(new DateOnly(2020, 3, 21), "Navro'z", isRecurring: true);
        var oneOff = Holiday.Create(new DateOnly(2026, 9, 1), "Mustaqillik", isRecurring: false);

        navruz.AppliesTo(new DateOnly(2026, 3, 21)).Should().BeTrue();
        navruz.AppliesTo(new DateOnly(2026, 3, 22)).Should().BeFalse();
        oneOff.AppliesTo(new DateOnly(2027, 9, 1)).Should().BeFalse();
    }

    [Fact]
    public void Davr_IshKuni_HaftaKuniVaBayramBoyicha()
    {
        var period = PracticePeriod.Create(
            "Ishlab chiqarish amaliyoti", Guid.CreateVersion7(), new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 30),
            Guid.CreateVersion7(), CheckInRules.Default, WorkDays.MondayToSaturday, 36, true);

        period.IsWorkDay(new DateOnly(2026, 9, 14), isHoliday: false).Should().BeTrue();   // Dushanba
        period.IsWorkDay(new DateOnly(2026, 9, 13), isHoliday: false).Should().BeFalse();  // Yakshanba
        period.IsWorkDay(new DateOnly(2026, 9, 14), isHoliday: true).Should().BeFalse();
        period.IsWorkDay(new DateOnly(2026, 11, 2), isHoliday: false).Should().BeFalse();  // davrdan tashqari
        period.Rules(100).Should().Be(CheckInRules.Default);
    }

    [Fact]
    public void Davr_GuruhIkkiMartaBiriktirilmaydi_YopilgachOzgarmaydi()
    {
        var period = PracticePeriod.Create(
            "Amaliyot", Guid.CreateVersion7(), new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 30),
            Guid.CreateVersion7(), CheckInRules.Default, WorkDays.MondayToFriday, 30, false);
        var group = Guid.CreateVersion7();

        period.AttachGroup(group);
        var twice = () => period.AttachGroup(group);
        twice.Should().Throw<ConflictException>();

        period.Activate();
        period.Close();
        var afterClose = () => period.AttachGroup(Guid.CreateVersion7());
        afterClose.Should().Throw<ConflictException>();
        period.Groups.Should().ContainSingle(g => g.StudentGroupId == group);
    }

    [Fact]
    public void WorkDays_ParseVaToCsv_Qaytariladi()
    {
        WorkDaysExtensions.Parse("1,2,3,4,5,6").Should().Be(WorkDays.MondayToSaturday);
        WorkDays.MondayToFriday.ToCsv().Should().Be("1,2,3,4,5");
        WorkDays.Sunday.Includes(DayOfWeek.Sunday).Should().BeTrue();
        WorkDays.MondayToFriday.Includes(DayOfWeek.Saturday).Should().BeFalse();
    }

    [Fact]
    public void PracticeTime_ToshkentKuniVaSoati()
    {
        // 13.09.2026 23:30 UTC = 14.09.2026 04:30 Toshkent
        var utc = new DateTimeOffset(2026, 9, 13, 23, 30, 0, TimeSpan.Zero);

        PracticeTime.LocalDate(utc).Should().Be(new DateOnly(2026, 9, 14));
        PracticeTime.LocalTime(utc).Should().Be(new TimeOnly(4, 30));
        PracticeTime.Hm(utc).Should().Be("04:30");
        PracticeTime.At(new DateOnly(2026, 9, 14), new TimeOnly(9, 0)).ToUniversalTime()
            .Should().Be(new DateTimeOffset(2026, 9, 14, 4, 0, 0, TimeSpan.Zero));
    }
}

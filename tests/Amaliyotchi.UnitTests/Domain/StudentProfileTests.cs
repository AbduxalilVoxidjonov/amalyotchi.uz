using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Students;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

public sealed class StudentProfileTests
{
    [Theory]
    [InlineData("341030", "341030")]
    [InlineData("  341221100123 ", "341221100123")]
    public void HemisId_Normallashtiriladi(string input, string expected)
    {
        var profile = StudentProfile.Create(Guid.CreateVersion7(), input, Guid.CreateVersion7());

        profile.HemisId.Should().Be(expected);
        profile.Status.Should().Be(StudentStatus.Active);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("34103O")]
    [InlineData("123456789012345678901")]
    [InlineData("")]
    public void HemisId_NotogriFormat_XatoBeradi(string input)
    {
        var act = () => StudentProfile.Create(Guid.CreateVersion7(), input, Guid.CreateVersion7());

        act.Should().Throw<DomainException>().WithMessage("*HEMIS*");
    }

    [Fact]
    public void Guruhsiz_XatoBeradi()
    {
        var act = () => StudentProfile.Create(Guid.CreateVersion7(), "341030", Guid.Empty);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void BitirganTalaba_Chetlashtirilmaydi()
    {
        var profile = StudentProfile.Create(Guid.CreateVersion7(), "341030", Guid.CreateVersion7());
        profile.Graduate();

        var act = () => profile.Suspend();

        act.Should().Throw<DomainException>();
        profile.IsActive.Should().BeFalse();
    }

    [Fact]
    public void TaklifTokeni_BirMartaIshlatiladi()
    {
        var profile = StudentProfile.Create(Guid.CreateVersion7(), "341030", Guid.CreateVersion7());
        var now = DateTimeOffset.UtcNow;
        profile.IssueInviteToken("INV_abc", now.AddDays(1));

        profile.ConsumeInviteToken("INV_abc", now);
        var again = () => profile.ConsumeInviteToken("INV_abc", now);

        profile.InviteToken.Should().BeNull();
        again.Should().Throw<DomainException>();
    }

    [Fact]
    public void TaklifTokeni_MuddatiOtgan_XatoBeradi()
    {
        var profile = StudentProfile.Create(Guid.CreateVersion7(), "341030", Guid.CreateVersion7());
        var now = DateTimeOffset.UtcNow;
        profile.IssueInviteToken("INV_abc", now.AddDays(-1));

        var act = () => profile.ConsumeInviteToken("INV_abc", now);

        act.Should().Throw<DomainException>().WithMessage("*muddati*");
    }

    // ---- O'z ish vaqti ----

    private static readonly DateOnly Today = new(2026, 10, 5);

    private static StudentProfile NewProfile() => StudentProfile.Create(Guid.CreateVersion7(), "341030", Guid.CreateVersion7());

    [Fact]
    public void IshVaqti_ErtadanKuchgaKiradi_BugunDavrSoatlari()
    {
        var profile = NewProfile();

        profile.SetWorkHours(new TimeOnly(13, 0), new TimeOnly(18, 0), Today);

        profile.WorkHoursEffectiveFrom.Should().Be(Today.AddDays(1));
        profile.HoursOn(Today).Should().BeNull("bugun hali davr soatlari");
        profile.HoursOn(Today.AddDays(1)).Should().Be((new TimeOnly(13, 0), new TimeOnly(18, 0)));
        profile.HoursOn(Today.AddDays(30)).Should().Be((new TimeOnly(13, 0), new TimeOnly(18, 0)));
    }

    [Fact]
    public void IshVaqti_AmaldaginiAlmashtirish_OldingisiErtagachaQoladi()
    {
        var profile = NewProfile();
        profile.SetWorkHours(new TimeOnly(13, 0), new TimeOnly(18, 0), Today);

        // Ertasi kuni (13:00–18:00 amalda) yangi soat — yana ertadan.
        var tomorrow = Today.AddDays(1);
        profile.SetWorkHours(new TimeOnly(8, 0), new TimeOnly(12, 0), tomorrow);

        profile.PreviousWorkStart.Should().Be(new TimeOnly(13, 0));
        profile.HoursOn(tomorrow).Should().Be((new TimeOnly(13, 0), new TimeOnly(18, 0)));
        profile.HoursOn(tomorrow.AddDays(1)).Should().Be((new TimeOnly(8, 0), new TimeOnly(12, 0)));
    }

    [Fact]
    public void IshVaqti_KuchgaKirmaganOzgarish_FaqatUAlmashadi()
    {
        var profile = NewProfile();
        profile.SetWorkHours(new TimeOnly(13, 0), new TimeOnly(18, 0), Today);
        profile.SetWorkHours(new TimeOnly(14, 0), new TimeOnly(19, 0), Today);

        profile.PreviousWorkStart.Should().BeNull("oldingi — davr soatlari saqlanadi");
        profile.HoursOn(Today).Should().BeNull();
        profile.HoursOn(Today.AddDays(1)).Should().Be((new TimeOnly(14, 0), new TimeOnly(19, 0)));
    }

    [Fact]
    public void IshVaqti_IkkalasiNull_DavrSoatlarigaErtadanQaytadi()
    {
        var profile = NewProfile();
        profile.SetWorkHours(new TimeOnly(13, 0), new TimeOnly(18, 0), Today);
        var later = Today.AddDays(3);

        profile.SetWorkHours(null, null, later);

        profile.WorkStart.Should().BeNull();
        profile.HoursOn(later).Should().Be((new TimeOnly(13, 0), new TimeOnly(18, 0)), "bugun eski soatlar");
        profile.HoursOn(later.AddDays(1)).Should().BeNull();
    }

    [Theory]
    [InlineData(9, 0, null, null, "*birga*")]
    [InlineData(null, null, 17, 0, "*birga*")]
    [InlineData(17, 0, 9, 0, "Ketish vaqti kelish vaqtidan keyin bo'lishi kerak.")]
    [InlineData(9, 0, 9, 0, "Ketish vaqti kelish vaqtidan keyin bo'lishi kerak.")]
    [InlineData(9, 0, 9, 59, "Ish vaqti kamida 1 soat bo'lishi kerak.")]
    public void IshVaqti_Notogri_XatoBeradi(int? sh, int? sm, int? eh, int? em, string message)
    {
        var profile = NewProfile();
        TimeOnly? start = sh is null ? null : new TimeOnly(sh.Value, sm!.Value);
        TimeOnly? end = eh is null ? null : new TimeOnly(eh.Value, em!.Value);

        var act = () => profile.SetWorkHours(start, end, Today);

        act.Should().Throw<DomainException>().WithMessage(message);
        profile.WorkHoursEffectiveFrom.Should().BeNull();
    }

    [Fact]
    public void IshVaqti_RoppaRosaBirSoat_Qabul()
    {
        var profile = NewProfile();

        profile.SetWorkHours(new TimeOnly(9, 0), new TimeOnly(10, 0), Today);

        profile.HoursOn(Today.AddDays(1)).Should().Be((new TimeOnly(9, 0), new TimeOnly(10, 0)));
    }
}

using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

public sealed class FacultyTests
{
    [Fact]
    public void Yaratish_KodniUppercaseGaOtkazadiVaFaolQiladi()
    {
        var faculty = Faculty.Create(" Axborot texnologiyalari ", " at ");

        faculty.Name.Should().Be("Axborot texnologiyalari");
        faculty.Code.Should().Be("AT");
        faculty.IsActive.Should().BeTrue();
    }

    [Fact]
    public void KodniOzgartirish_TrimVaUppercase()
    {
        var faculty = Faculty.Create("Axborot texnologiyalari", "AT");

        faculty.ChangeCode(" it ");

        faculty.Code.Should().Be("IT");
    }

    [Fact]
    public void KodniOzgartirish_BoshKod_XatoBeradi()
    {
        var faculty = Faculty.Create("Axborot texnologiyalari", "AT");

        var act = () => faculty.ChangeCode("   ");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void FaolHolat_ActivateVaDeactivate()
    {
        var faculty = Faculty.Create("Axborot texnologiyalari", "AT");

        faculty.Deactivate();
        faculty.IsActive.Should().BeFalse();

        faculty.Activate();
        faculty.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Ochirish_FaqatOziniArxivlaydi_KafedralarniKaskadQilmaydi()
    {
        var faculty = Faculty.Create("Axborot texnologiyalari", "AT");
        var department = faculty.AddDepartment("Dasturiy injiniring kafedrasi", "SE");
        var now = DateTimeOffset.UtcNow;

        faculty.Delete(now);

        faculty.IsDeleted.Should().BeTrue();
        faculty.DeletedAt.Should().Be(now);
        department.IsDeleted.Should().BeFalse();
        department.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void AddDepartment_TakroriyKod_ZiddiyatBeradi()
    {
        var faculty = Faculty.Create("Axborot texnologiyalari", "AT");
        faculty.AddDepartment("Dasturiy injiniring kafedrasi", "SE");

        var act = () => faculty.AddDepartment("Boshqa nom", "se");

        act.Should().Throw<ConflictException>();
    }
}

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
    public void Ochirish_OziniVaYonalishlariniArxivlaydi()
    {
        var faculty = Faculty.Create("Axborot texnologiyalari", "AT");
        var direction1 = faculty.AddDirection("Dasturiy injiniring", "60610500");
        var direction2 = faculty.AddDirection("Kompyuter injiniringi", "60610400");
        var now = DateTimeOffset.UtcNow;

        faculty.Delete(now);

        faculty.IsDeleted.Should().BeTrue();
        faculty.DeletedAt.Should().Be(now);
        direction1.IsDeleted.Should().BeTrue();
        direction1.DeletedAt.Should().Be(now);
        direction2.IsDeleted.Should().BeTrue();
        direction2.DeletedAt.Should().Be(now);
    }
}

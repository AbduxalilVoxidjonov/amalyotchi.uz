using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

public sealed class OrganizationTests
{
    private static readonly Guid YearId = Guid.CreateVersion7();

    [Fact]
    public void Fakultet_TakroriyYonalishKodi_ZiddiyatBeradi()
    {
        var faculty = Faculty.Create("Axborot texnologiyalari", "AT");
        faculty.AddDirection("Dasturiy injiniring", "60610100");

        var act = () => faculty.AddDirection("Boshqa nom", "60610100");

        act.Should().Throw<ConflictException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Guruh_NotogriKurs_XatoBeradi(int course)
    {
        var faculty = Faculty.Create("AT", "AT");
        var direction = faculty.AddDirection("Dasturiy injiniring", "60610100");

        var act = () => direction.AddGroup("412-22", course, YearId);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Guruh_KeyingiKursgaKochiriladi()
    {
        var faculty = Faculty.Create("AT", "AT");
        var direction = faculty.AddDirection("Dasturiy injiniring", "60610100");
        var group = direction.AddGroup("412-22", 3, YearId);

        var promoted = group.PromoteTo(Guid.CreateVersion7());

        promoted.Course.Should().Be(4);
        promoted.Name.Should().Be("412-22");
    }

    [Fact]
    public void OquvYili_TugashSanasiOldinBolsa_XatoBeradi()
    {
        var act = () => AcademicYear.Create(
            "2026-2027",
            new DateOnly(2027, 9, 1),
            new DateOnly(2026, 9, 1));

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OquvYili_Arxivlash_SoftDeleteHolatiniOzgartirmaydi(bool isDeleted)
    {
        var year = AcademicYear.Create("2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        year.Activate();
        year.IsDeleted = isDeleted;

        year.Archive();

        year.IsActive.Should().BeFalse();
        year.IsDeleted.Should().Be(isDeleted);
    }
}

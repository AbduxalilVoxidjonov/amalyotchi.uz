using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

/// <summary>O'quv yili — mustaqil, ierarxiyaga bog'liq emas. Fakultet/kafedra/yo'nalish/guruh
/// testlari mos ravishda <see cref="FacultyTests"/>, <see cref="DepartmentTests"/>,
/// <see cref="DirectionTests"/>, <see cref="StudentGroupTests"/> da.</summary>
public sealed class OrganizationTests
{
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

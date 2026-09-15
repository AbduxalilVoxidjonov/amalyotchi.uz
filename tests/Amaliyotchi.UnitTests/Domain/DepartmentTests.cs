using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

public sealed class DepartmentTests
{
    private static Faculty CreateFaculty() => Faculty.Create("Axborot texnologiyalari", "AT");

    [Fact]
    public void Yaratish_KodniUppercaseGaOtkazadiVaFaolQiladi()
    {
        var faculty = CreateFaculty();

        var department = faculty.AddDepartment(" Dasturiy injiniring kafedrasi ", " se ");

        department.Name.Should().Be("Dasturiy injiniring kafedrasi");
        department.Code.Should().Be("SE");
        department.FacultyId.Should().Be(faculty.Id);
        department.IsActive.Should().BeTrue();
    }

    [Fact]
    public void AddDepartment_TakroriyKod_CaseInsensitive_ZiddiyatBeradi()
    {
        var faculty = CreateFaculty();
        faculty.AddDepartment("Dasturiy injiniring kafedrasi", "SE");

        var act = () => faculty.AddDepartment("Boshqa nom", "se");

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void Update_NomVaKodOzgaradi()
    {
        var faculty = CreateFaculty();
        var department = faculty.AddDepartment("Eski nom", "OLD");

        department.Update(" Yangi nom ", " new ");

        department.Name.Should().Be("Yangi nom");
        department.Code.Should().Be("NEW");
    }

    [Fact]
    public void Update_BoshNom_XatoBeradi()
    {
        var faculty = CreateFaculty();
        var department = faculty.AddDepartment("Nom", "COD");

        var act = () => department.Update("   ", "COD");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void FaolHolat_ActivateVaDeactivate()
    {
        var faculty = CreateFaculty();
        var department = faculty.AddDepartment("Nom", "COD");

        department.Deactivate();
        department.IsActive.Should().BeFalse();

        department.Activate();
        department.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Ochirish_FaqatOziniArxivlaydi_YonalishlarniKaskadQilmaydi()
    {
        var faculty = CreateFaculty();
        var department = faculty.AddDepartment("Nom", "COD");
        var direction = department.AddDirection("Dasturiy injiniring", "60610500");
        var now = DateTimeOffset.UtcNow;

        department.Delete(now);

        department.IsDeleted.Should().BeTrue();
        department.DeletedAt.Should().Be(now);
        direction.IsDeleted.Should().BeFalse();
    }
}

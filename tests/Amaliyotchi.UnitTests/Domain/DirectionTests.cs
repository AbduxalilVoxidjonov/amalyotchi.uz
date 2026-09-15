using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

public sealed class DirectionTests
{
    private static readonly Guid YearId = Guid.CreateVersion7();

    private static Department CreateDepartment()
        => Faculty.Create("Axborot texnologiyalari", "AT").AddDepartment("Dasturiy injiniring kafedrasi", "SE");

    [Fact]
    public void Yaratish_KodniUppercaseGaOtkazadiVaFaolQiladi()
    {
        var department = CreateDepartment();

        var direction = department.AddDirection(" Dasturiy injiniring ", " d1 ");

        direction.Name.Should().Be("Dasturiy injiniring");
        direction.Code.Should().Be("D1");
        direction.DepartmentId.Should().Be(department.Id);
        direction.IsActive.Should().BeTrue();
    }

    [Fact]
    public void AddDirection_TakroriyKod_CaseInsensitive_ZiddiyatBeradi()
    {
        var department = CreateDepartment();
        department.AddDirection("Dasturiy injiniring", "60610100");

        var act = () => department.AddDirection("Boshqa nom", "60610100");

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void Update_NomVaKodOzgaradi()
    {
        var department = CreateDepartment();
        var direction = department.AddDirection("Eski nom", "OLD");

        direction.Update(" Yangi nom ", " new ");

        direction.Name.Should().Be("Yangi nom");
        direction.Code.Should().Be("NEW");
    }

    [Fact]
    public void FaolHolat_ActivateVaDeactivate()
    {
        var department = CreateDepartment();
        var direction = department.AddDirection("Nom", "COD");

        direction.Deactivate();
        direction.IsActive.Should().BeFalse();

        direction.Activate();
        direction.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Ochirish_FaqatOziniArxivlaydi_GuruhlarniKaskadQilmaydi()
    {
        var department = CreateDepartment();
        var direction = department.AddDirection("Nom", "COD");
        var group = direction.AddGroup("412-22", 3, YearId);
        var now = DateTimeOffset.UtcNow;

        direction.Delete(now);

        direction.IsDeleted.Should().BeTrue();
        direction.DeletedAt.Should().Be(now);
        group.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void AddGroup_TakroriyNomVaOquvYili_ZiddiyatBeradi()
    {
        var department = CreateDepartment();
        var direction = department.AddDirection("Dasturiy injiniring", "60610100");
        direction.AddGroup("412-22", 3, YearId);

        var act = () => direction.AddGroup("412-22", 3, YearId);

        act.Should().Throw<ConflictException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void AddGroup_NotogriKurs_XatoBeradi(int course)
    {
        var department = CreateDepartment();
        var direction = department.AddDirection("Dasturiy injiniring", "60610100");

        var act = () => direction.AddGroup("412-22", course, YearId);

        act.Should().Throw<DomainException>();
    }
}

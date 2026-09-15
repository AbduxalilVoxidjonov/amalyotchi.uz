using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

public sealed class StudentGroupTests
{
    private static readonly Guid YearId = Guid.CreateVersion7();

    private static Direction CreateDirection()
        => Faculty.Create("Axborot texnologiyalari", "AT")
            .AddDepartment("Dasturiy injiniring kafedrasi", "SE")
            .AddDirection("Dasturiy injiniring", "60610100");

    [Fact]
    public void Yaratish_FaolQiladi()
    {
        var direction = CreateDirection();

        var group = direction.AddGroup("412-22", 3, YearId);

        group.Name.Should().Be("412-22");
        group.Course.Should().Be(3);
        group.DirectionId.Should().Be(direction.Id);
        group.AcademicYearId.Should().Be(YearId);
        group.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Update_NomVaKursOzgaradi()
    {
        var direction = CreateDirection();
        var group = direction.AddGroup("412-22", 3, YearId);

        group.Update(" 413-22 ", 4);

        group.Name.Should().Be("413-22");
        group.Course.Should().Be(4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Update_NotogriKurs_XatoBeradi(int course)
    {
        var direction = CreateDirection();
        var group = direction.AddGroup("412-22", 3, YearId);

        var act = () => group.Update("412-22", course);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void FaolHolat_ActivateVaDeactivate()
    {
        var direction = CreateDirection();
        var group = direction.AddGroup("412-22", 3, YearId);

        group.Deactivate();
        group.IsActive.Should().BeFalse();

        group.Activate();
        group.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Guruh_NotogriKurs_XatoBeradi(int course)
    {
        var direction = CreateDirection();

        var act = () => direction.AddGroup("412-22", course, YearId);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Guruh_KeyingiKursgaKochiriladi()
    {
        var direction = CreateDirection();
        var group = direction.AddGroup("412-22", 3, YearId);

        var promoted = group.PromoteTo(Guid.CreateVersion7());

        promoted.Course.Should().Be(4);
        promoted.Name.Should().Be("412-22");
    }
}

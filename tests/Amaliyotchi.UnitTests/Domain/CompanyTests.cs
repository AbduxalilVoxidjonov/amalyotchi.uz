using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

public sealed class CompanyTests
{
    private static readonly GeoPoint Tashkent = new(41.311081, 69.240562);

    private static Company Create(string tin = "123456789", int radius = 200)
        => Company.Create("UzInfoCom", tin, "IT", "Toshkent, Amir Temur 1", Tashkent, radius, "Islomov B.", "90 123 45 67");

    [Theory]
    [InlineData("123456789", "123456789")]
    [InlineData("123 456 789", "123456789")]
    [InlineData("123-456-789", "123456789")]
    public void Stir_BittaKorinishgaKeltiriladi(string input, string expected)
    {
        Create(tin: input).Tin.Should().Be(expected);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("12345678a")]
    [InlineData("")]
    public void Stir_Notogri_XatoBeradi(string input)
    {
        var act = () => Create(tin: input);

        act.Should().Throw<DomainException>().WithMessage("*STIR*");
    }

    [Theory]
    [InlineData(49)]
    [InlineData(1001)]
    public void Radius_OraliqdanTashqari_XatoBeradi(int radius)
    {
        var act = () => Create(radius: radius);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SetRadius_OzgarganBolsaTrue()
    {
        var company = Create(radius: 200);

        company.SetRadius(200).Should().BeFalse();
        company.SetRadius(120).Should().BeTrue();
        company.RadiusM.Should().Be(120);
    }

    [Fact]
    public void Telefon_E164gaKeltiriladi_MentorIxtiyoriy()
    {
        var company = Create();

        company.SupervisorPhone.Should().Be("+998901234567");
        company.MentorName.Should().BeNull();
        company.MentorPhone.Should().BeNull();
        company.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(91, 69)]
    [InlineData(-91, 69)]
    [InlineData(41, 181)]
    [InlineData(41, -181)]
    [InlineData(double.NaN, 69)]
    public void GeoPoint_NotogriKoordinata_XatoBeradi(double lat, double lng)
    {
        var act = () => new GeoPoint(lat, lng);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void GeoPoint_HaversineMasofa_TaxminanTogri()
    {
        // Amir Temur xiyoboni → Toshkent teleminorasi ≈ 5,1 km
        var tower = new GeoPoint(41.348, 69.284);

        var distance = Tashkent.DistanceMetersTo(tower);

        distance.Should().BeInRange(5_000, 5_700);
        Tashkent.DistanceMetersTo(Tashkent).Should().Be(0);
    }

    [Fact]
    public void Relocate_NuqtaniOzgartiradi()
    {
        var company = Create();

        company.Relocate(41.35, 69.28);

        company.Location.Should().Be(new GeoPoint(41.35, 69.28));
    }
}

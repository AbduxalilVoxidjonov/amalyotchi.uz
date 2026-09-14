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
}

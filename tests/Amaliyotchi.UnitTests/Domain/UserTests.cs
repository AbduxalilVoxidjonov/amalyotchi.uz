using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

public sealed class UserTests
{
    private static readonly Guid FacultyId = Guid.CreateVersion7();

    [Fact]
    public void CreateWithPassword_TalabaUchun_XatoBeradi()
    {
        var act = () => User.CreateWithPassword("Aliyev Ali", "901234567", "hash", UserRole.Student, FacultyId);

        act.Should().Throw<DomainException>()
            .WithMessage("*Telegram*");
    }

    [Fact]
    public void CreateWithPassword_TyutorFakultetsiz_XatoBeradi()
    {
        var act = () => User.CreateWithPassword("Aliyev Ali", "901234567", "hash", UserRole.Tutor);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("901234567")]
    [InlineData("+998 90 123 45 67")]
    [InlineData("998901234567")]
    public void TelefonRaqami_BittaKorinishgaKeltiriladi(string input)
    {
        var user = User.CreateWithPassword("Aliyev Ali", input, "hash", UserRole.Tutor, FacultyId);

        user.PhoneNumber.Should().Be("+998901234567");
    }

    [Fact]
    public void LinkTelegram_BoshqaAkkauntBilan_ZiddiyatBeradi()
    {
        var student = User.CreateStudent("Karimov Bek", FacultyId);
        student.LinkTelegram(111, "901234567");

        var act = () => student.LinkTelegram(222, "901234567");

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void Deactivate_BarchaRefreshTokenlarniBekorQiladi()
    {
        var user = User.CreateWithPassword("Aliyev Ali", "901234567", "hash", UserRole.Admin);
        user.IssueRefreshToken("token-1", DateTimeOffset.UtcNow.AddDays(7), "127.0.0.1");

        user.Deactivate();

        user.RefreshTokens.Should().OnlyContain(t => !t.IsActive(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PruneRefreshTokens_FaqatMuddatiOtganlarniOlibTashlaydi()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.CreateWithPassword("Aliyev Ali", "901234567", "hash", UserRole.Admin);
        user.IssueRefreshToken("expired", now.AddDays(-1), null);
        user.IssueRefreshToken("active", now.AddDays(7), null);
        user.IssueRefreshToken("revoked", now.AddDays(7), null).Revoke(now, "test");

        user.PruneRefreshTokens(now);

        user.RefreshTokens.Select(t => t.Token).Should().BeEquivalentTo("active", "revoked");
    }

    [Fact]
    public void FaolBolmaganHisob_TokenOlaOlmaydi()
    {
        var user = User.CreateWithPassword("Aliyev Ali", "901234567", "hash", UserRole.Admin);
        user.Deactivate();

        var act = () => user.IssueRefreshToken("token", DateTimeOffset.UtcNow.AddDays(1), null);

        act.Should().Throw<ForbiddenException>();
    }
}

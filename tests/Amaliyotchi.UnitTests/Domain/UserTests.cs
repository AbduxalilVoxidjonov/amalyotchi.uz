using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

public sealed class UserTests
{
    private static readonly Guid FacultyId = Guid.CreateVersion7();
    private const string HemisId = "100000000001";

    [Fact]
    public void CreateWithPassword_TalabaUchun_XatoBeradi()
    {
        var act = () => User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Student);

        act.Should().Throw<DomainException>()
            .WithMessage("*Telegram*");
    }

    [Fact]
    public void AssignToFaculty_Talaba_Ozgaradi_Adminga_XatoBeradi()
    {
        var student = User.CreateStudent("Karimov Bek", FacultyId);
        var admin = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Admin);
        var other = Guid.CreateVersion7();

        student.AssignToFaculty(other);
        var act = () => admin.AssignToFaculty(other);

        student.FacultyId.Should().Be(other);
        act.Should().Throw<DomainException>();
        admin.FacultyId.Should().BeNull();
    }

    // ---------- Bosh admin ----------

    [Fact]
    public void MakeSuperAdmin_FaqatFaolAdmin()
    {
        var admin = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Admin);
        admin.IsSuperAdmin.Should().BeFalse("sukut bo'yicha oddiy admin");

        admin.MakeSuperAdmin();
        admin.IsSuperAdmin.Should().BeTrue();

        var student = User.CreateStudent("Karimov Bek", FacultyId);
        ((Action)student.MakeSuperAdmin).Should().Throw<DomainException>();

        var blocked = User.CreateWithPassword("Valiyev Vali", "100000000009", null, "hash", UserRole.Admin);
        blocked.Deactivate();
        ((Action)blocked.MakeSuperAdmin).Should().Throw<DomainException>();
    }

    [Fact]
    public void BoshAdmin_Bloklanmaydi_TokenlariSaqlanadi()
    {
        var admin = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Admin);
        admin.MakeSuperAdmin();
        admin.IssueRefreshToken("token-1", DateTimeOffset.UtcNow.AddDays(7), null);

        var act = () => admin.Deactivate();

        act.Should().Throw<ConflictException>().WithMessage(User.SuperAdminBlockMessage);
        admin.IsActive.Should().BeTrue();
        admin.RefreshTokens.Should().OnlyContain(t => t.IsActive(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void SetTemporaryPassword_Admin_MustChangePassword()
    {
        var admin = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Admin);
        admin.MustChangePassword.Should().BeFalse();

        admin.SetTemporaryPassword("temp-hash");

        admin.PasswordHash.Should().Be("temp-hash");
        admin.MustChangePassword.Should().BeTrue();
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("abc123456789")]
    [InlineData("")]
    public void CreateWithPassword_NotogriHemisId_XatoBeradi(string input)
    {
        var act = () => User.CreateWithPassword("Aliyev Ali", input, "901234567", "hash", UserRole.Admin);

        act.Should().Throw<DomainException>()
            .WithMessage("*HEMIS ID*");
    }

    [Theory]
    [InlineData("901234567")]
    [InlineData("+998 90 123 45 67")]
    [InlineData("998901234567")]
    public void TelefonRaqami_BittaKorinishgaKeltiriladi(string input)
    {
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, input, "hash", UserRole.Admin);

        user.PhoneNumber.Should().Be("+998901234567");
    }

    [Fact]
    public void CreateWithPassword_Telefonsiz_PhoneNumberNull()
    {
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, phoneNumber: null, "hash", UserRole.Admin);

        user.PhoneNumber.Should().BeNull();
        user.HemisId.Should().Be(HemisId);
    }

    [Fact]
    public void ChangeHemisId_Normallashtiradi_BirXil_Talaba_NotogriFormat_Xato()
    {
        var admin = User.CreateWithPassword("Aliyev Ali", HemisId, phoneNumber: null, "hash", UserRole.Admin);

        admin.ChangeHemisId(" 200000000002 ");
        admin.HemisId.Should().Be("200000000002");

        ((Action)(() => admin.ChangeHemisId("200000000002"))).Should().Throw<DomainException>()
            .WithMessage(User.SameHemisIdMessage);
        ((Action)(() => admin.ChangeHemisId("12ab"))).Should().Throw<DomainException>().WithMessage("*HEMIS ID*");
        ((Action)(() => User.CreateStudent("Karimov Bek", FacultyId).ChangeHemisId("300000000003")))
            .Should().Throw<DomainException>();
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
    public void LinkTelegram_TelefonsizMavjudTelefonniSaqlaydi_VaIdempotent()
    {
        var student = User.CreateStudent("Karimov Bek", FacultyId, "901234567");
        var phone = student.PhoneNumber;

        student.LinkTelegram(111);
        student.LinkTelegram(111);

        student.TelegramUserId.Should().Be(111);
        student.PhoneNumber.Should().Be(phone);
    }

    [Fact]
    public void LinkTelegram_TelefonsizVaTelefoniYoqTalaba_NullQoladi()
    {
        var student = User.CreateStudent("Karimov Bek", FacultyId);

        student.LinkTelegram(111);

        student.TelegramUserId.Should().Be(111);
        student.PhoneNumber.Should().BeNull();
    }

    [Fact]
    public void Deactivate_BarchaRefreshTokenlarniBekorQiladi()
    {
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Admin);
        user.IssueRefreshToken("token-1", DateTimeOffset.UtcNow.AddDays(7), "127.0.0.1");

        user.Deactivate();

        user.RefreshTokens.Should().OnlyContain(t => !t.IsActive(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RevokeRefreshTokens_HisobFaolQoladi_TokenlarBekorBoladi()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Admin);
        user.IssueRefreshToken("token-1", now.AddDays(7), null);
        user.IssueRefreshToken("token-2", now.AddDays(7), null);

        user.RevokeRefreshTokens(now, "Parol tiklandi");

        user.IsActive.Should().BeTrue();
        user.RefreshTokens.Should().HaveCount(2).And.OnlyContain(t => t.RevokedAt == now && t.RevokedReason == "Parol tiklandi");
    }

    [Fact]
    public void RevokeRefreshTokens_AvvalBekorQilinganTokenniQaytaYozmaydi()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Admin);
        var earlier = now.AddHours(-1);
        user.IssueRefreshToken("old", now.AddDays(7), null).Revoke(earlier, "Chiqdi");

        user.RevokeRefreshTokens(now, "Parol tiklandi");

        var token = user.RefreshTokens.Single();
        token.RevokedAt.Should().Be(earlier);
        token.RevokedReason.Should().Be("Chiqdi");
    }

    [Theory]
    [InlineData("90 765 43 21", "+998907654321")]
    [InlineData("+998 90 765 43 21", "+998907654321")]
    public void ChangePhoneNumber_Normallashtiradi(string input, string expected)
    {
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Admin);

        user.ChangePhoneNumber(input);

        user.PhoneNumber.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangePhoneNumber_Bosh_NullQiladi(string? input)
    {
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Admin);

        user.ChangePhoneNumber(input);

        user.PhoneNumber.Should().BeNull();
    }

    [Fact]
    public void ChangePhoneNumber_NotogriFormat_XatoBeradi()
    {
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Admin);

        var act = () => user.ChangePhoneNumber("12345");

        act.Should().Throw<DomainException>().WithMessage("*Telefon*");
        user.PhoneNumber.Should().Be("+998901234567");
    }

    [Fact]
    public void ChangePhoneNumber_Talabaga_XatoBeradi()
    {
        var student = User.CreateStudent("Karimov Bek", FacultyId);

        var act = () => student.ChangePhoneNumber("901234567");

        act.Should().Throw<DomainException>().WithMessage("*Telegram*");
    }

    [Fact]
    public void PruneRefreshTokens_FaqatMuddatiOtganlarniOlibTashlaydi()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Admin);
        user.IssueRefreshToken("expired", now.AddDays(-1), null);
        user.IssueRefreshToken("active", now.AddDays(7), null);
        user.IssueRefreshToken("revoked", now.AddDays(7), null).Revoke(now, "test");

        user.PruneRefreshTokens(now);

        user.RefreshTokens.Select(t => t.Token).Should().BeEquivalentTo("active", "revoked");
    }

    [Fact]
    public void FaolBolmaganHisob_TokenOlaOlmaydi()
    {
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Admin);
        user.Deactivate();

        var act = () => user.IssueRefreshToken("token", DateTimeOffset.UtcNow.AddDays(1), null);

        act.Should().Throw<ForbiddenException>();
    }
}

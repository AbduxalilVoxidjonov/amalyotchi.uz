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
        var act = () => User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Student, FacultyId);

        act.Should().Throw<DomainException>()
            .WithMessage("*Telegram*");
    }

    [Fact]
    public void CreateWithPassword_TyutorFakultetsiz_XatoBeradi()
    {
        var act = () => User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Tutor);

        act.Should().Throw<DomainException>().WithMessage(User.FacultyRequiredMessage);
    }

    [Fact]
    public void CreateWithPassword_TyutorBittaFakultet_FaculiesVaFacultyIdToldiriladi()
    {
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Tutor, FacultyId);

        user.FacultyId.Should().Be(FacultyId);
        var link = user.Faculties.Should().ContainSingle().Subject;
        link.FacultyId.Should().Be(FacultyId);
        link.TutorUserId.Should().Be(user.Id);
    }

    [Fact]
    public void CreateWithPassword_TyutorBirNechaFakultet_BirinchisiAsosiy()
    {
        var second = Guid.CreateVersion7();

        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Tutor, [FacultyId, second]);

        user.FacultyId.Should().Be(FacultyId);
        user.Faculties.Select(f => f.FacultyId).Should().Equal(FacultyId, second);
    }

    [Fact]
    public void CreateWithPassword_AdminFakultetBilan_XatoBeradi()
    {
        var act = () => User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Admin, FacultyId);

        act.Should().Throw<DomainException>().WithMessage("*Admin*");
    }

    // ---------- SetFaculties ----------

    [Fact]
    public void SetFaculties_Bosh_XatoBeradi_ToplamOzgarmaydi()
    {
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Tutor, FacultyId);

        var act = () => user.SetFaculties([]);

        act.Should().Throw<DomainException>().WithMessage(User.FacultyRequiredMessage);
        user.Faculties.Select(f => f.FacultyId).Should().Equal(FacultyId);
        user.FacultyId.Should().Be(FacultyId);
    }

    [Fact]
    public void SetFaculties_BoshGuid_XatoBeradi()
    {
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Tutor, FacultyId);

        var act = () => user.SetFaculties([FacultyId, Guid.Empty]);

        act.Should().Throw<DomainException>();
        user.Faculties.Should().ContainSingle();
    }

    [Fact]
    public void SetFaculties_Takrorlar_BittagaKeltiriladi()
    {
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Tutor, FacultyId);
        var second = Guid.CreateVersion7();

        user.SetFaculties([second, FacultyId, second, FacultyId]);

        user.Faculties.Select(f => f.FacultyId).Should().BeEquivalentTo([FacultyId, second]);
        user.Faculties.Should().HaveCount(2);
    }

    [Fact]
    public void SetFaculties_FacultyId_RoyxatdagiBirinchisi()
    {
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Tutor, FacultyId);
        var second = Guid.CreateVersion7();

        user.SetFaculties([second, FacultyId]);

        user.FacultyId.Should().Be(second, "asosiy fakultet — ro'yxatning birinchisi");
    }

    [Fact]
    public void SetFaculties_OlibTashlashVaQoshish_MavjudYozuvSaqlanadi()
    {
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Tutor, FacultyId);
        var originalLinkId = user.Faculties.Single().Id;
        var second = Guid.CreateVersion7();
        var third = Guid.CreateVersion7();

        user.SetFaculties([FacultyId, second]);
        user.Faculties.Select(f => f.FacultyId).Should().Equal(FacultyId, second);
        user.Faculties.First().Id.Should().Be(originalLinkId, "mavjud bog'lanish qayta yaratilmaydi");

        user.SetFaculties([third, second]);
        user.Faculties.Select(f => f.FacultyId).Should().BeEquivalentTo([second, third]);
        user.Faculties.Should().NotContain(f => f.FacultyId == FacultyId, "ro'yxatda yo'q — olib tashlandi");
        user.FacultyId.Should().Be(third);
    }

    [Fact]
    public void SetFaculties_Talabaga_XatoBeradi()
    {
        var student = User.CreateStudent("Karimov Bek", FacultyId);

        var act = () => student.SetFaculties([FacultyId]);

        act.Should().Throw<DomainException>().WithMessage("*tyutorga*");
        student.Faculties.Should().BeEmpty();
        student.FacultyId.Should().Be(FacultyId);
    }

    [Fact]
    public void SetFaculties_Adminga_XatoBeradi()
    {
        var admin = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Admin);

        var act = () => admin.SetFaculties([FacultyId]);

        act.Should().Throw<DomainException>();
        admin.FacultyId.Should().BeNull();
    }

    [Fact]
    public void AssignToFaculty_Talaba_Ozgaradi_Tyutorga_XatoBeradi()
    {
        var student = User.CreateStudent("Karimov Bek", FacultyId);
        var tutor = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Tutor, FacultyId);
        var other = Guid.CreateVersion7();

        student.AssignToFaculty(other);
        var act = () => tutor.AssignToFaculty(other);

        student.FacultyId.Should().Be(other);
        act.Should().Throw<DomainException>();
        tutor.FacultyId.Should().Be(FacultyId);
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
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, input, "hash", UserRole.Tutor, FacultyId);

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
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Tutor, FacultyId);
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
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Tutor, FacultyId);
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
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Tutor, FacultyId);

        user.ChangePhoneNumber(input);

        user.PhoneNumber.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangePhoneNumber_Bosh_NullQiladi(string? input)
    {
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Tutor, FacultyId);

        user.ChangePhoneNumber(input);

        user.PhoneNumber.Should().BeNull();
    }

    [Fact]
    public void ChangePhoneNumber_NotogriFormat_XatoBeradi()
    {
        var user = User.CreateWithPassword("Aliyev Ali", HemisId, "901234567", "hash", UserRole.Tutor, FacultyId);

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

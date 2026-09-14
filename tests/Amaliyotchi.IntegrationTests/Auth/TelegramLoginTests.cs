using System.Net;
using Amaliyotchi.Application.Features.Auth;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed class TelegramLoginTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task TogriInitData_200_TalabaKiradi_VaMeIshlaydi()
    {
        var student = await Factory.CreateStudentAsync();

        var (client, auth) = await Factory.TelegramLoginAsync(student.TelegramId!.Value);

        auth.User.Id.Should().Be(student.Id);
        auth.User.Role.Should().Be(UserRole.Student);
        auth.RefreshToken.Should().NotBeNullOrEmpty();

        // Kontrakt v2: talaba uchun guruh/kurs/HEMIS ID (StudentProfile ⟕ StudentGroup).
        auth.User.GroupId.Should().Be(student.GroupId);
        auth.User.GroupName.Should().StartWith("G-");
        auth.User.Course.Should().Be(3);
        auth.User.HemisId.Should().HaveLength(12);

        var me = await client.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await me.Content.ReadAsync<UserSummaryDto>();
        summary!.Id.Should().Be(student.Id);
        summary.GroupId.Should().Be(student.GroupId);
        summary.GroupName.Should().Be(auth.User.GroupName);
        summary.Course.Should().Be(3);
        summary.HemisId.Should().Be(auth.User.HemisId);

        // Refresh token talaba uchun ham rotatsiya qilinadi
        var refresh = await Factory.CreateClient().PostJsonAsync("/api/auth/refresh", new { auth.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NotogriHash_403()
    {
        var student = await Factory.CreateStudentAsync();
        var initData = TelegramInitDataFactory.Create(
            student.TelegramId!.Value, ApiFactory.TelegramBotToken, DateTimeOffset.UtcNow,
            hashOverride: new string('0', 64));

        var response = await Factory.CreateClient().PostJsonAsync("/api/auth/telegram", new { initData });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BoshqaBotTokeniBilanImzolangan_403()
    {
        var student = await Factory.CreateStudentAsync();
        var initData = TelegramInitDataFactory.Create(
            student.TelegramId!.Value, "9999999999:BOSHQA-bot-token", DateTimeOffset.UtcNow);

        var response = await Factory.CreateClient().PostJsonAsync("/api/auth/telegram", new { initData });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EskiAuthDate_403()
    {
        var student = await Factory.CreateStudentAsync();
        var initData = TelegramInitDataFactory.Create(
            student.TelegramId!.Value, ApiFactory.TelegramBotToken, DateTimeOffset.UtcNow.AddHours(-25));

        var response = await Factory.CreateClient().PostJsonAsync("/api/auth/telegram", new { initData });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RoyxatdanOtmaganTelegramId_403()
    {
        var unknownId = Random.Shared.NextInt64(10_000_000_000, 90_000_000_000);
        var initData = TelegramInitDataFactory.Create(unknownId, ApiFactory.TelegramBotToken, DateTimeOffset.UtcNow);

        var response = await Factory.CreateClient().PostJsonAsync("/api/auth/telegram", new { initData });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Hisob topilmadi");
    }

    [Fact]
    public async Task FaolBolmaganTalaba_403()
    {
        var student = await Factory.CreateStudentAsync(active: false);
        var initData = TelegramInitDataFactory.Create(student.TelegramId!.Value, ApiFactory.TelegramBotToken, DateTimeOffset.UtcNow);

        var response = await Factory.CreateClient().PostJsonAsync("/api/auth/telegram", new { initData });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BoshInitData_400_Errors()
    {
        var response = await Factory.CreateClient().PostJsonAsync("/api/auth/telegram", new { initData = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"errors\"");
    }
}

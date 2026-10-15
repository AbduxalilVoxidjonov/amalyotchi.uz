using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Messages;
using Amaliyotchi.Application.Features.Auth;
using Amaliyotchi.Application.Features.Student.Messages;
using Amaliyotchi.IntegrationTests.Admin;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary>Talabaning xabarlar qutisi — <c>/api/student/messages</c>: ro'yxat (yangi birinchi, faqat o'ziniki), o'qilmaganlar
/// soni, bittasini / hammasini o'qilgan deb belgilash. Xabar yaratilgan zahoti qutida (Telegram dispetcherini kutmaydi);
/// Telegram bog'lamagan talaba ham oladi.</summary>
[Collection(ApiCollection.Name)]
public sealed class StudentMessagesTests(ApiFixture fixture)
{
    private const string Url = "/api/student/messages";
    private const string AdminUrl = "/api/admin/messages";

    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task List_YangiBirinchi_FaqatOziniki_Sahifalash_TelegramniKutmaydi()
    {
        var a = await Factory.CreateStudentAsync();
        var b = await Factory.CreateStudentAsync();
        var admin = await Factory.LoginAsAdminAsync();
        var clientA = await Factory.LoginAsStudentAsync(a);
        var clientB = await Factory.LoginAsStudentAsync(b);

        var start = DateTimeOffset.UtcNow.AddMinutes(-10);
        MessageSummary m1, m2, m3;
        try
        {
            Factory.Clock.Set(start);
            m1 = await SendAsync(admin, "Birinchi", a.Id);
            Factory.Clock.Set(start.AddMinutes(1));
            m2 = await SendAsync(admin, "Ikkinchi — ikkalasiga", a.Id, b.Id);
            Factory.Clock.Set(start.AddMinutes(2));
            m3 = await SendAsync(admin, "Faqat B ga", b.Id);
        }
        finally
        {
            Factory.Clock.Reset();
        }

        // Dispetcher ishga tushmagan (Telegram navbatda) — baribir qutida.
        var pageA = await clientA.GetPagedAsync<StudentMessageDto>(Url);
        pageA.Total.Should().Be(2);
        pageA.Items.Select(x => x.Id).Should().Equal(m2.Id, m1.Id);
        pageA.Items[0].Text.Should().Be("Ikkinchi — ikkalasiga");
        pageA.Items[0].CreatedAt.Should().BeCloseTo(start.AddMinutes(1), TimeSpan.FromMilliseconds(1));
        pageA.Items.Should().OnlyContain(x => x.ReadAt == null);
        (pageA.Page, pageA.PageSize).Should().Be((1, 20));

        var pageB = await clientB.GetPagedAsync<StudentMessageDto>(Url);
        pageB.Items.Select(x => x.Id).Should().Equal(m3.Id, m2.Id);

        var second = await clientA.GetPagedAsync<StudentMessageDto>($"{Url}?page=2&pageSize=1");
        (second.Total, second.Page, second.PageSize).Should().Be((2, 2, 1));
        second.Items.Select(x => x.Id).Should().Equal(m1.Id);

        // JSON shakli: camelCase, createdAt — offset bilan ISO, readAt — null.
        using var json = JsonDocument.Parse(await clientA.GetStringAsync($"{Url}?pageSize=1"));
        json.RootElement.GetProperty("total").GetInt32().Should().Be(2);
        var item = json.RootElement.GetProperty("items")[0];
        item.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["id", "text", "createdAt", "readAt"]);
        item.GetProperty("id").GetGuid().Should().Be(m2.Id);
        item.GetProperty("readAt").ValueKind.Should().Be(JsonValueKind.Null);
        var createdAt = item.GetProperty("createdAt").GetString()!;
        createdAt.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$");
        DateTimeOffset.Parse(createdAt).Offset.Should().Be(TimeSpan.Zero);

        (await UnreadAsync(clientA)).Should().Be(2);
        (await UnreadAsync(clientB)).Should().Be(2);
    }

    [Fact]
    public async Task Read_Idempotent_BegonaVaYoq404_ReadAll_AdminDeliveriesdaReadAt()
    {
        var a = await Factory.CreateStudentAsync();
        var b = await Factory.CreateStudentAsync();
        var admin = await Factory.LoginAsAdminAsync();
        var clientA = await Factory.LoginAsStudentAsync(a);

        var m1 = await SendAsync(admin, "Bir", a.Id);
        var m2 = await SendAsync(admin, "Ikki", a.Id);
        var m3 = await SendAsync(admin, "Uch", a.Id);
        var foreign = await SendAsync(admin, "Begona", b.Id);

        (await clientA.PostAsync($"{Url}/{m1.Id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var firstRead = (await clientA.GetPagedAsync<StudentMessageDto>(Url)).Items.Single(x => x.Id == m1.Id).ReadAt;
        firstRead.Should().NotBeNull();
        (await UnreadAsync(clientA)).Should().Be(2);

        // Takror — 204, birinchi o'qilgan vaqt o'zgarmaydi.
        (await clientA.PostAsync($"{Url}/{m1.Id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await clientA.GetPagedAsync<StudentMessageDto>(Url)).Items.Single(x => x.Id == m1.Id).ReadAt.Should().Be(firstRead);

        // Begona va mavjud bo'lmagan xabar → 404; begona xabar o'qilmay qoladi.
        (await clientA.PostAsync($"{Url}/{foreign.Id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await clientA.PostAsync($"{Url}/{Guid.NewGuid()}/read", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var foreignRead = await Factory.WithDbAsync(db => db.BroadcastDeliveries
            .Where(d => d.MessageId == foreign.Id).Select(d => d.ReadAt).SingleAsync());
        foreignRead.Should().BeNull();

        // Admin yetkazishlar ro'yxatida o'qilganlik ko'rinadi.
        var rows = await admin.GetPagedAsync<MessageDeliveryRow>($"{AdminUrl}/{m1.Id}/deliveries");
        rows.Items.Should().ContainSingle().Which.ReadAt.Should().Be(firstRead);

        (await clientA.PostAsync($"{Url}/read-all", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UnreadAsync(clientA)).Should().Be(0);
        var all = await clientA.GetPagedAsync<StudentMessageDto>(Url);
        all.Items.Select(x => x.Id).Should().Equal(m3.Id, m2.Id, m1.Id);
        all.Items.Should().OnlyContain(x => x.ReadAt != null);
        all.Items.Single(x => x.Id == m1.Id).ReadAt.Should().Be(firstRead, "read-all avval o'qilganlarga tegmaydi");

        // Bo'sh holatda ham 204; boshqa talabaning xabari o'qilmagan qoladi.
        (await clientA.PostAsync($"{Url}/read-all", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Factory.WithDbAsync(db => db.BroadcastDeliveries
            .Where(d => d.MessageId == foreign.Id).Select(d => d.ReadAt).SingleAsync())).Should().BeNull();
    }

    [Fact]
    public async Task TelegramBoglamaganTalaba_XabarniQutidaOladi()
    {
        var student = await Factory.CreateStudentAsync(linkTelegram: false);
        var admin = await Factory.LoginAsAdminAsync();
        var client = await LoginWithPasswordAsync(admin, student.Id);

        (await UnreadAsync(client)).Should().Be(0);
        var empty = await client.GetPagedAsync<StudentMessageDto>(Url);
        (empty.Total, empty.Items.Count).Should().Be((0, 0));

        var message = await SendAsync(admin, "Telegram'siz ham yetadi", student.Id);
        message.AppOnly.Should().Be(1);

        var page = await client.GetPagedAsync<StudentMessageDto>(Url);
        page.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            message.Id,
            Text = "Telegram'siz ham yetadi",
            ReadAt = (DateTimeOffset?)null
        });
        (await UnreadAsync(client)).Should().Be(1);

        (await client.PostAsync($"{Url}/{message.Id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UnreadAsync(client)).Should().Be(0);
    }

    [Fact]
    public async Task FaqatTalaba_Admin403_Anonim401()
    {
        var admin = await Factory.LoginAsAdminAsync();
        (await admin.GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.GetAsync($"{Url}/unread-count")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsync($"{Url}/read-all", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsync($"{Url}/{Guid.NewGuid()}/read", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var anonymous = Factory.CreateClient();
        (await anonymous.GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync($"{Url}/unread-count")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync($"{Url}/read-all", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ------------------------------------------------------------------ yordamchilar

    private static async Task<MessageSummary> SendAsync(HttpClient admin, string text, params Guid[] userIds)
    {
        var response = await admin.PostJsonAsync(AdminUrl, new
        {
            text,
            attachAppButton = false,
            audience = new { kind = "selected", userIds }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadAsync<MessageSummary>())!;
    }

    private static async Task<int> UnreadAsync(HttpClient client)
    {
        var response = await client.GetAsync($"{Url}/unread-count");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal("count");
        return json.RootElement.GetProperty("count").GetInt32();
    }

    /// <summary>Telegram'siz talaba: admin parol o'rnatadi, talaba HEMIS ID + parol bilan kiradi (mobil ilova oqimi).</summary>
    private async Task<HttpClient> LoginWithPasswordAsync(HttpClient admin, Guid studentId)
    {
        const string password = "Ilova-Parol-1";
        (await admin.PostJsonAsync($"/api/admin/students/{studentId}/password", new { password }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var hemisId = await Factory.WithDbAsync(db =>
            db.StudentProfiles.Where(p => p.UserId == studentId).Select(p => p.HemisId).SingleAsync());

        var login = await Factory.CreateClient().PostJsonAsync("/api/auth/login", new { hemisId, password });
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());
        var auth = (await login.Content.ReadAsync<AuthResultDto>())!;

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}

using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary><c>GET/POST /api/student/leave-requests</c>: yaratish, kesishuv 409, validatsiya, ko'lam.</summary>
[Collection(ApiCollection.Name)]
public sealed class LeaveRequestsTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Yaratish_201_RoyxatdaKoradi()
    {
        var scene = await Factory.CreateSceneAsync();
        var from = Factory.LocalToday().AddDays(2);
        var to = from.AddDays(1);

        var response = await scene.Client.PostJsonAsync("/api/student/leave-requests",
            new { dateFrom = from, dateTo = to, reason = "Shifokor ko'rigi — ma'lumotnoma bor", attachmentName = "malumotnoma.pdf" });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var dto = await response.Content.ReadAsync<LeaveRequestDto>();
        dto!.DateFrom.Should().Be(from);
        dto.DateTo.Should().Be(to);
        dto.Status.Should().Be(LeaveRequestStatus.Pending);
        dto.Comment.Should().BeNull();
        dto.Document.Should().NotBeNull();
        dto.Document!.Name.Should().Be("malumotnoma.pdf");
        dto.Document.Url.Should().BeNull("fayl yuklanmagan — faqat nom");
        dto.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        var list = await (await scene.Client.GetAsync("/api/student/leave-requests")).Content.ReadAsync<List<LeaveRequestDto>>();
        list.Should().ContainSingle(l => l.Id == dto.Id);

        using var json = JsonDocument.Parse(await (await scene.Client.GetAsync("/api/student/leave-requests")).Content.ReadAsStringAsync());
        json.RootElement[0].GetProperty("status").GetString().Should().Be("pending");
        json.RootElement[0].GetProperty("dateFrom").GetString().Should().Be(from.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task Kesishuv_409()
    {
        var scene = await Factory.CreateSceneAsync();
        var from = Factory.LocalToday().AddDays(3);
        (await scene.Client.PostJsonAsync("/api/student/leave-requests",
            new { dateFrom = from, dateTo = from.AddDays(2), reason = "Oilaviy sabab, tushuntirish xati" })).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await scene.Client.PostJsonAsync("/api/student/leave-requests",
            new { dateFrom = from.AddDays(2), dateTo = from.AddDays(4), reason = "Yana bir sabab, batafsil" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("allaqachon");
    }

    [Fact]
    public async Task Validatsiya_400_ErrorsReasonDateTo()
    {
        var scene = await Factory.CreateSceneAsync();
        var from = Factory.LocalToday().AddDays(3);

        var response = await scene.Client.PostJsonAsync("/api/student/leave-requests",
            new { dateFrom = from, dateTo = from.AddDays(-1), reason = "qisqa" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors");
        errors.TryGetProperty("Reason", out _).Should().BeTrue();
        errors.TryGetProperty("DateTo", out _).Should().BeTrue();
    }

    [Fact]
    public async Task DavrTashqarisi_400()
    {
        var scene = await Factory.CreateSceneAsync();
        var from = scene.Period.EndDate.AddDays(1);

        var response = await scene.Client.PostJsonAsync("/api/student/leave-requests",
            new { dateFrom = from, dateTo = from, reason = "Davr tugagandan keyingi kun" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("davri ichida");
    }

    [Fact]
    public async Task Royxat_FaqatOziniki()
    {
        var sceneA = await Factory.CreateSceneAsync();
        var studentB = await Factory.CreateStudentAsync(group: sceneA.Group);
        var clientB = await Factory.LoginAsStudentAsync(studentB);
        var from = Factory.LocalToday().AddDays(5);
        (await sceneA.Client.PostJsonAsync("/api/student/leave-requests",
            new { dateFrom = from, dateTo = from, reason = "A talabaning sababi, batafsil" })).StatusCode.Should().Be(HttpStatusCode.Created);

        var listB = await (await clientB.GetAsync("/api/student/leave-requests")).Content.ReadAsync<List<LeaveRequestDto>>();

        listB.Should().BeEmpty();
        // B uchun kesishuv yo'q — o'z so'rovini bera oladi.
        (await clientB.PostJsonAsync("/api/student/leave-requests",
            new { dateFrom = from, dateTo = from, reason = "B talabaning sababi, batafsil" })).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Tyutor_403()
    {
        var tutor = await Factory.LoginAsTutorAsync();
        (await tutor.GetAsync("/api/student/leave-requests")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

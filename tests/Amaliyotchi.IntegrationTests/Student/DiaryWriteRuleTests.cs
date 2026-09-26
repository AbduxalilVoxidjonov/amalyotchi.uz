using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary>Kundalik yozish qoidasi: <c>POST /api/student/diary</c> va <c>GET /api/student/today</c>
/// (<c>canWriteDiary</c>/<c>diaryBlockedReason</c>) — faqat ochiq, bugunni o'z ichiga olgan davrda.</summary>
[Collection(ApiCollection.Name)]
public sealed class DiaryWriteRuleTests(ApiFixture fixture)
{
    private const string EndedMessage = "Amaliyot davri yakunlangan — yangi kundalik yozuvi qo'shib bo'lmaydi.";
    private const string NotStartedMessage = "Amaliyot davri hali boshlanmagan.";
    private const string AlreadySubmittedMessage = "Bugungi hisobot allaqachon yuborilgan.";

    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task FaolDavr_CanWriteTrue_Post201_KeyinCanWriteFalse()
    {
        var scene = await Factory.CreateSceneAsync();

        using (var json = await GetTodayJsonAsync(scene.Client))
        {
            json.RootElement.GetProperty("canWriteDiary").GetBoolean().Should().BeTrue();
            json.RootElement.GetProperty("diaryBlockedReason").ValueKind.Should().Be(JsonValueKind.Null);
        }

        using var form = StudentTestData.DiaryForm(StudentTestData.LongText());
        var response = await scene.Client.PostAsync("/api/student/diary", form);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        // Yuborilgandan keyin POST 409 beradi — today ham shuni aytadi.
        var today = await GetTodayAsync(scene.Client);
        today.CanWriteDiary.Should().BeFalse();
        today.DiaryBlockedReason.Should().Be(AlreadySubmittedMessage);
    }

    [Fact]
    public async Task YopilganDavr_SanasiOtmagan_Post400_CanWriteFalse()
    {
        var scene = await Factory.CreateSceneAsync();
        await ClosePeriodAsync(scene.Period.Id);

        await AssertBlockedAsync(scene.Client, EndedMessage);
    }

    [Fact]
    public async Task TugaganDavr_Post400_CanWriteFalse()
    {
        var today = Factory.LocalToday();
        var scene = await Factory.CreateSceneAsync(
            period: (group, tutorId) => Factory.CreatePeriodAtAsync(group, tutorId, today, startDaysAgo: 40, endDaysAhead: -10));

        await AssertBlockedAsync(scene.Client, EndedMessage);
    }

    [Fact]
    public async Task BoshlanmaganDavr_Post400_CanWriteFalse()
    {
        var today = Factory.LocalToday();
        var scene = await Factory.CreateSceneAsync(
            period: (group, tutorId) => Factory.CreatePeriodAtAsync(group, tutorId, today, startDaysAgo: -5, endDaysAhead: 30));

        await AssertBlockedAsync(scene.Client, NotStartedMessage);
    }

    [Fact]
    public async Task QaytaYozish_YakunlanganDavrda_400_EskiYozuvlarRoyxatda()
    {
        var scene = await Factory.CreateSceneAsync();
        using var first = StudentTestData.DiaryForm(StudentTestData.LongText());
        var created = await (await scene.Client.PostAsync("/api/student/diary", first)).Content.ReadAsync<DiaryEntryDto>();
        await Factory.WithDbAsync(async db =>
        {
            var entry = await db.DiaryEntries.SingleAsync(d => d.Id == created!.Id);
            entry.RequestRewrite(scene.Tutor.Id, "Batafsilroq yozing", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });

        // Ochiq davrda rewrite holatidagi yozuvni qayta yozish mumkin.
        (await GetTodayAsync(scene.Client)).CanWriteDiary.Should().BeTrue();

        await ClosePeriodAsync(scene.Period.Id);
        await AssertBlockedAsync(scene.Client, EndedMessage);

        var stored = await Factory.WithDbAsync(db => db.DiaryEntries.AsNoTracking().SingleAsync(d => d.Id == created!.Id));
        stored.Status.Should().Be(DiaryStatus.Rewrite, "yakunlangan davrda yozuv o'zgarmaydi");

        // Eski yozuvlar ro'yxatda qoladi.
        var list = await (await scene.Client.GetAsync("/api/student/diary")).Content.ReadAsync<List<DiaryEntryDto>>();
        list.Should().ContainSingle(e => e.Id == created!.Id);
    }

    private async Task AssertBlockedAsync(HttpClient client, string message)
    {
        using var form = StudentTestData.DiaryForm(StudentTestData.LongText());
        var response = await client.PostAsync("/api/student/diary", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using (var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
            problem.RootElement.GetProperty("detail").GetString().Should().Be(message);

        using var json = await GetTodayJsonAsync(client);
        json.RootElement.GetProperty("canWriteDiary").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("diaryBlockedReason").GetString().Should().Be(message);
    }

    private Task ClosePeriodAsync(Guid periodId) =>
        Factory.WithDbAsync(async db =>
        {
            var period = await db.PracticePeriods.FirstAsync(p => p.Id == periodId);
            period.Close();
            await db.SaveChangesAsync();
        });

    private static async Task<TodayDto> GetTodayAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/student/today");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadAsync<TodayDto>())!;
    }

    private static async Task<JsonDocument> GetTodayJsonAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/student/today");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}

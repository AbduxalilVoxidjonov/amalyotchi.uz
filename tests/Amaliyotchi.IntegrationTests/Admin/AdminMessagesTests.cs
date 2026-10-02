using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Messages;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Messaging;
using Amaliyotchi.Infrastructure.Messaging;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary>"Xabarlar" — <c>/api/admin/messages</c>: qabul qiluvchilar, yuborish (auditoriya resolve), tarix, yetkazishlar,
/// qayta yuborish va fon dispetcheri (testda <see cref="BroadcastDispatcher.RunOnceAsync"/> qo'lda, Telegram — <see cref="FakeTelegramMessenger"/>).
/// Umumiy bazada boshqa testlarning talabalari ham bor — har test o'z guruhi/ism tegi bilan ajratadi.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminMessagesTests(ApiFixture fixture)
{
    private const string Url = "/api/admin/messages";

    private ApiFactory Factory => fixture.Factory;
    private BroadcastDispatcher Dispatcher => Factory.Services.GetRequiredService<BroadcastDispatcher>();
    private FakeTelegramMessenger Telegram => Factory.Messenger;

    // ------------------------------------------------------------------ qabul qiluvchilar

    [Fact]
    public async Task Recipients_FaqatUlanganFaolTalabalar_FiltrQSahifalashBotBlocked()
    {
        var tag = Tag();
        var group = await Factory.CreateGroupAsync(course: 2);
        var otherGroup = await Factory.CreateGroupAsync(facultyId: group.FacultyId, course: 4);
        var a = await Factory.CreateStudentAsync(group: group, fullName: $"{tag} Anvar");
        var b = await Factory.CreateStudentAsync(group: group, fullName: $"{tag} Botir");
        var c = await Factory.CreateStudentAsync(group: otherGroup, fullName: $"{tag} Ziyod");
        await Factory.CreateStudentAsync(group: group, fullName: $"{tag} Ulanmagan", linkTelegram: false);
        await Factory.CreateStudentAsync(group: group, fullName: $"{tag} Faolsiz", active: false);
        await Factory.WithDbAsync(async db =>
        {
            var user = await db.Users.FirstAsync(u => u.Id == b.Id);
            user.MarkBotBlocked(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });
        var bHemis = await HemisIdAsync(b.Id);

        var admin = await Factory.LoginAsAdminAsync();
        async Task<Paged<MessageRecipientRow>> Get(string query)
            => await admin.GetPagedAsync<MessageRecipientRow>($"{Url}/recipients?q={tag}&pageSize=100{query}");

        var all = await Get("");
        all.Total.Should().Be(3, "ulanmagan va faolsiz talaba ro'yxatda yo'q");
        all.Items.Select(x => x.UserId).Should().Equal(a.Id, b.Id, c.Id); // FISH bo'yicha
        var rowB = all.Items.Single(x => x.UserId == b.Id);
        rowB.BotBlocked.Should().BeTrue();
        rowB.TelegramUserId.Should().Be(b.TelegramId);
        rowB.HemisId.Should().Be(bHemis);
        rowB.GroupName.Should().Be(group.GroupName);
        rowB.Course.Should().Be(2);
        rowB.FacultyName.Should().NotBeNullOrEmpty();
        rowB.DirectionName.Should().NotBeNullOrEmpty();
        all.Items.Single(x => x.UserId == a.Id).BotBlocked.Should().BeFalse();

        (await Get($"&groupId={group.GroupId}")).Items.Select(x => x.UserId).Should().BeEquivalentTo([a.Id, b.Id]);
        (await Get("&course=4")).Items.Select(x => x.UserId).Should().Equal(c.Id);
        (await Get($"&facultyId={group.FacultyId}")).Total.Should().Be(3);
        (await Get($"&directionId={otherGroup.DirectionId}")).Items.Select(x => x.UserId).Should().Equal(c.Id);

        // q: Telegram ID (aniq) va HEMIS ID — teg'siz.
        var byTelegram = await admin.GetPagedAsync<MessageRecipientRow>($"{Url}/recipients?q={a.TelegramId}");
        byTelegram.Items.Select(x => x.UserId).Should().Equal(a.Id);
        var byHemis = await admin.GetPagedAsync<MessageRecipientRow>($"{Url}/recipients?q={bHemis}");
        byHemis.Items.Select(x => x.UserId).Should().Contain(b.Id);

        var page = await admin.GetPagedAsync<MessageRecipientRow>($"{Url}/recipients?q={tag}&pageSize=2&page=2");
        page.Total.Should().Be(3);
        page.Items.Select(x => x.UserId).Should().Equal(c.Id);

        // JSON shakli: telegramUserId — son, botBlocked — boolean.
        using var json = JsonDocument.Parse(await admin.GetStringAsync($"{Url}/recipients?q={a.TelegramId}"));
        var item = json.RootElement.GetProperty("items")[0];
        item.GetProperty("telegramUserId").ValueKind.Should().Be(JsonValueKind.Number);
        item.GetProperty("botBlocked").ValueKind.Should().Be(JsonValueKind.False);
        item.TryGetProperty("telegramLinkedAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task RecipientGroups_FaqatUlanganTalabasiBorGuruhlar()
    {
        var withLinked = await Factory.CreateGroupAsync(course: 3);
        var onlyUnlinked = await Factory.CreateGroupAsync(facultyId: withLinked.FacultyId, course: 3);
        await Factory.CreateStudentAsync(group: withLinked);
        await Factory.CreateStudentAsync(group: onlyUnlinked, linkTelegram: false);

        var admin = await Factory.LoginAsAdminAsync();
        var response = await admin.GetAsync($"{Url}/recipients/groups?facultyId={withLinked.FacultyId}&course=3");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var groups = (await response.Content.ReadAsync<List<MessageRecipientGroupOption>>())!;

        groups.Should().ContainSingle().Which.Should().Be(new MessageRecipientGroupOption(withLinked.GroupId, withLinked.GroupName));
        (await admin.GetAsync($"{Url}/recipients/groups?course=9")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------ yaratish: validatsiya va resolve

    [Fact]
    public async Task Create_Validatsiya_400_Tyutor403()
    {
        var student = await Factory.CreateStudentAsync();
        var admin = await Factory.LoginAsAdminAsync();
        var selected = new { kind = "selected", userIds = new[] { student.Id } };

        (await admin.PostJsonAsync(Url, new { text = "   ", attachAppButton = false, audience = selected }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostJsonAsync(Url, new { text = new string('a', 4001), attachAppButton = false, audience = selected }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostJsonAsync(Url, new { text = "Salom", attachAppButton = false, audience = new { kind = "selected", userIds = Array.Empty<Guid>() } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostJsonAsync(Url, new { text = "Salom", attachAppButton = false, audience = new { kind = "selected", userIds = Enumerable.Range(0, 5001).Select(_ => Guid.NewGuid()) } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostJsonAsync(Url, new { text = "Salom", attachAppButton = false }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostJsonAsync(Url, new { text = "Salom", attachAppButton = false, audience = new { kind = "everyone" } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Auditoriya bo'sh: mavjud bo'lmagan va ulanmagan id'lar.
        var unlinked = await Factory.CreateStudentAsync(linkTelegram: false);
        var empty = await admin.PostJsonAsync(Url, new
        {
            text = "Salom",
            attachAppButton = false,
            audience = new { kind = "selected", userIds = new[] { Guid.NewGuid(), unlinked.Id } }
        });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Detail(empty)).Should().Be("Tanlangan auditoriyada Telegram ulangan talaba yo'q.");

        var emptyFilter = await admin.PostJsonAsync(Url, new
        {
            text = "Salom",
            attachAppButton = false,
            audience = new { kind = "filter", groupId = Guid.NewGuid() }
        });
        emptyFilter.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 4000 belgi (trim'dan keyin) — chegarada qabul qilinadi.
        var max = await admin.PostJsonAsync(Url, new { text = "  " + new string('b', 4000) + "  ", attachAppButton = false, audience = selected });
        max.StatusCode.Should().Be(HttpStatusCode.Created, await max.Content.ReadAsStringAsync());
        (await max.Content.ReadAsync<MessageSummary>())!.Text.Should().HaveLength(4000);

        var tutor = await Factory.LoginAsTutorAsync();
        (await tutor.PostJsonAsync(Url, new { text = "Salom", attachAppButton = false, audience = selected }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tutor.GetAsync($"{Url}/recipients")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tutor.GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Selected_TakrorVaUlanmaganTashlanadi_201LocationVaAudit()
    {
        var a = await Factory.CreateStudentAsync();
        var unlinked = await Factory.CreateStudentAsync(linkTelegram: false);
        var adminUser = await Factory.CreateAdminAsync("Xabar Admin");
        var admin = await Factory.LoginAsync(adminUser);

        var response = await admin.PostJsonAsync(Url, new
        {
            text = "  Ertaga 9:00 da yig'ilish  ",
            attachAppButton = true,
            audience = new { kind = "selected", userIds = new[] { a.Id, a.Id, unlinked.Id, Guid.NewGuid() } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var summary = (await response.Content.ReadAsync<MessageSummary>())!;
        response.Headers.Location!.ToString().Should().EndWith($"{Url}/{summary.Id}");

        summary.Text.Should().Be("Ertaga 9:00 da yig'ilish");
        summary.AttachAppButton.Should().BeTrue();
        summary.AudienceLabel.Should().Be("Tanlangan: 1 ta talaba");
        summary.CreatedByName.Should().Be("Xabar Admin");
        summary.Status.Should().Be(BroadcastMessageStatus.Queued);
        (summary.Total, summary.Pending, summary.Sent, summary.Failed, summary.Blocked).Should().Be((1, 1, 0, 0, 0));

        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
            json.RootElement.GetProperty("status").GetString().Should().Be("queued");

        var deliveries = await Factory.WithDbAsync(db => db.BroadcastDeliveries.Where(d => d.MessageId == summary.Id).ToListAsync());
        deliveries.Should().ContainSingle().Which.ChatId.Should().Be(a.TelegramId);

        var audit = await Factory.WithDbAsync(db => db.AuditLogs.SingleAsync(l =>
            l.Action == AuditAction.BroadcastMessageCreated && l.EntityId == summary.Id.ToString()));
        audit.UserId.Should().Be(adminUser.Id);
        audit.Changes.Should().Contain("Tanlangan: 1 ta talaba");

        var get = await admin.GetAsync($"{Url}/{summary.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        (await get.Content.ReadAsync<MessageSummary>()).Should().BeEquivalentTo(summary);
    }

    [Fact]
    public async Task Create_Filter_VaAll_ServerdaResolveVaLabel()
    {
        var group = await Factory.CreateGroupAsync(course: 3);
        var s1 = await Factory.CreateStudentAsync(group: group);
        var s2 = await Factory.CreateStudentAsync(group: group);
        await Factory.CreateStudentAsync(group: group, linkTelegram: false);
        await Factory.CreateStudentAsync(group: group, active: false);
        var admin = await Factory.LoginAsAdminAsync();

        var filter = await admin.PostJsonAsync(Url, new
        {
            text = "Guruhga",
            attachAppButton = false,
            audience = new { kind = "filter", facultyId = group.FacultyId, groupId = group.GroupId, course = 3 }
        });
        filter.StatusCode.Should().Be(HttpStatusCode.Created, await filter.Content.ReadAsStringAsync());
        var filtered = (await filter.Content.ReadAsync<MessageSummary>())!;
        filtered.Total.Should().Be(2);
        var facultyName = await Factory.WithDbAsync(db => db.Faculties.Where(f => f.Id == group.FacultyId).Select(f => f.Name).SingleAsync());
        filtered.AudienceLabel.Should().Be($"{facultyName} · 3-kurs · {group.GroupName}");
        var recipients = await Factory.WithDbAsync(db =>
            db.BroadcastDeliveries.Where(d => d.MessageId == filtered.Id).Select(d => d.RecipientUserId).ToListAsync());
        recipients.Should().BeEquivalentTo([s1.Id, s2.Id]);

        var expectedAll = await Factory.WithDbAsync(db => db.Users.CountAsync(u =>
            u.Role == UserRole.Student && u.IsActive && u.TelegramUserId != null));
        var all = await admin.PostJsonAsync(Url, new { text = "Hammaga", attachAppButton = false, audience = new { kind = "all" } });
        all.StatusCode.Should().Be(HttpStatusCode.Created);
        var allSummary = (await all.Content.ReadAsync<MessageSummary>())!;
        allSummary.AudienceLabel.Should().Be("Barcha ulanganlar");
        allSummary.Total.Should().Be(expectedAll);

        // Tarix: yangi birinchi.
        var history = await admin.GetPagedAsync<MessageSummary>($"{Url}?pageSize=2");
        history.Items.Select(m => m.Id).Should().Equal(allSummary.Id, filtered.Id);
        history.Total.Should().BeGreaterThanOrEqualTo(2);

        // "all" xabari boshqa testlarga xalaqit bermasin — darhol yuborib tugatiladi.
        await DrainAsync();
    }

    // ------------------------------------------------------------------ dispetcher

    [Fact]
    public async Task Dispatcher_Sent_Blocked_Transient5UrinishdanKeyinFailed_RetryVaDeliveries()
    {
        await DrainAsync();
        var tag = Tag();
        var group = await Factory.CreateGroupAsync();
        var ok = await Factory.CreateStudentAsync(group: group, fullName: $"{tag} A-Yetadi");
        var blocked = await Factory.CreateStudentAsync(group: group, fullName: $"{tag} B-Bloklagan");
        var flaky = await Factory.CreateStudentAsync(group: group, fullName: $"{tag} C-Tarmoq");

        Telegram.Script(blocked.TelegramId!.Value,
            new TelegramSendResult(TelegramSendOutcome.Blocked, Error: TelegramMessenger.BlockedError));
        Telegram.Script(flaky.TelegramId!.Value, Enumerable.Range(0, BroadcastDelivery.MaxAttempts)
            .Select(_ => new TelegramSendResult(TelegramSendOutcome.TransientError, Error: "Telegram xatosi (502)")).ToArray());

        var admin = await Factory.LoginAsAdminAsync();
        var summary = await CreateAsync(admin, "Yetkazish testi", true, ok.Id, blocked.Id, flaky.Id);

        var start = Factory.Clock.UtcNow;
        try
        {
            var first = await Dispatcher.RunOnceAsync();
            first.Sent.Should().Be(1);
            first.Blocked.Should().Be(1);

            var rows = await DeliveriesAsync(summary.Id);
            var okRow = rows.Single(d => d.RecipientUserId == ok.Id);
            okRow.Status.Should().Be(BroadcastDeliveryStatus.Sent);
            okRow.SentAt.Should().NotBeNull();
            okRow.TelegramMessageId.Should().NotBeNull();
            Telegram.CallsTo(ok.TelegramId!.Value).Should().ContainSingle()
                .Which.Should().Be(new FakeTelegramMessenger.Call(ok.TelegramId.Value, "Yetkazish testi", true));

            var blockedRow = rows.Single(d => d.RecipientUserId == blocked.Id);
            blockedRow.Status.Should().Be(BroadcastDeliveryStatus.Blocked);
            blockedRow.Error.Should().Be(TelegramMessenger.BlockedError);
            (await BotBlockedAtAsync(blocked.Id)).Should().NotBeNull("User.TelegramBotBlockedAt belgilanadi");

            var flakyRow = rows.Single(d => d.RecipientUserId == flaky.Id);
            flakyRow.Status.Should().Be(BroadcastDeliveryStatus.Pending);
            flakyRow.Attempts.Should().Be(1);
            flakyRow.NextAttemptAt.Should().BeAfter(start);

            var mid = await GetSummaryAsync(admin, summary.Id);
            mid.Status.Should().Be(BroadcastMessageStatus.Sending);
            (mid.Sent, mid.Blocked, mid.Pending).Should().Be((1, 1, 1));

            // Backoff kutilmasa — olinmaydi.
            await Dispatcher.RunOnceAsync();
            (await DeliveriesAsync(summary.Id)).Single(d => d.RecipientUserId == flaky.Id).Attempts.Should().Be(1);

            // Soatni oldinga surib — qolgan urinishlar; 5-dan keyin failed.
            var at = start;
            for (var attempt = 2; attempt <= BroadcastDelivery.MaxAttempts; attempt++)
            {
                at = at.AddHours(1);
                Factory.Clock.Set(at);
                await Dispatcher.RunOnceAsync();
            }

            flakyRow = (await DeliveriesAsync(summary.Id)).Single(d => d.RecipientUserId == flaky.Id);
            flakyRow.Status.Should().Be(BroadcastDeliveryStatus.Failed);
            flakyRow.Attempts.Should().Be(BroadcastDelivery.MaxAttempts);
            flakyRow.Error.Should().Be("Telegram xatosi (502)");
            Telegram.CallsTo(flaky.TelegramId.Value).Should().HaveCount(BroadcastDelivery.MaxAttempts);
        }
        finally
        {
            Factory.Clock.Reset();
        }

        var done = await GetSummaryAsync(admin, summary.Id);
        done.Status.Should().Be(BroadcastMessageStatus.Completed);
        (done.Total, done.Sent, done.Failed, done.Blocked, done.Pending).Should().Be((3, 1, 1, 1, 0));

        // Yetkazishlar ro'yxati: holat filtri, q, FISH tartibi, xato matni.
        var failedRows = await admin.GetPagedAsync<MessageDeliveryRow>($"{Url}/{summary.Id}/deliveries?status=failed");
        failedRows.Items.Should().ContainSingle().Which.UserId.Should().Be(flaky.Id);
        var blockedRows = await admin.GetPagedAsync<MessageDeliveryRow>($"{Url}/{summary.Id}/deliveries?status=blocked");
        blockedRows.Items.Should().ContainSingle().Which.Error.Should().Be(TelegramMessenger.BlockedError);
        var allRows = await admin.GetPagedAsync<MessageDeliveryRow>($"{Url}/{summary.Id}/deliveries");
        allRows.Items.Select(r => r.UserId).Should().Equal(ok.Id, blocked.Id, flaky.Id);
        allRows.Items[0].SentAt.Should().NotBeNull();
        allRows.Items[0].HemisId.Should().Be(await HemisIdAsync(ok.Id));
        (await admin.GetPagedAsync<MessageDeliveryRow>($"{Url}/{summary.Id}/deliveries?q=bloklagan")).Items
            .Select(r => r.UserId).Should().Equal(blocked.Id);
        (await admin.GetAsync($"{Url}/{summary.Id}/deliveries?status=unknown")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Retry: faqat failed → pending (blocked emas).
        var retry = await admin.PostAsync($"{Url}/{summary.Id}/retry", null);
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        var retried = (await retry.Content.ReadAsync<MessageSummary>())!;
        (retried.Failed, retried.Blocked, retried.Pending, retried.Status).Should().Be((0, 1, 1, BroadcastMessageStatus.Sending));
        var requeued = (await DeliveriesAsync(summary.Id)).Single(d => d.RecipientUserId == flaky.Id);
        (requeued.Attempts, requeued.Error).Should().Be((0, (string?)null));

        await DrainAsync();
        var final = await GetSummaryAsync(admin, summary.Id);
        (final.Sent, final.Blocked, final.Failed, final.Status).Should().Be((2, 1, 0, BroadcastMessageStatus.Completed));

        // failed yo'q — retry baribir 200, o'zgarishsiz.
        var noop = await admin.PostAsync($"{Url}/{summary.Id}/retry", null);
        noop.StatusCode.Should().Be(HttpStatusCode.OK);
        (await noop.Content.ReadAsync<MessageSummary>()).Should().BeEquivalentTo(final);

        // Bloklagan talabaga keyingi xabar yetib borsa — blok belgisi tozalanadi.
        await CreateAsync(admin, "Yana", false, blocked.Id);
        await DrainAsync();
        (await BotBlockedAtAsync(blocked.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Dispatcher_429_RetryAfterBilanKechiktiradi_UrinishHisoblanmaydi()
    {
        await DrainAsync();
        var student = await Factory.CreateStudentAsync();
        Telegram.Script(student.TelegramId!.Value,
            new TelegramSendResult(TelegramSendOutcome.RateLimited, RetryAfter: TimeSpan.FromSeconds(30), Error: "429"));

        var admin = await Factory.LoginAsAdminAsync();
        var summary = await CreateAsync(admin, "Cheklov", false, student.Id);
        var start = DateTimeOffset.UtcNow;

        try
        {
            Factory.Clock.Set(start);
            var result = await Dispatcher.RunOnceAsync();
            result.PauseFor.Should().Be(TimeSpan.FromSeconds(30), "global pauza");

            var row = (await DeliveriesAsync(summary.Id)).Single();
            row.Status.Should().Be(BroadcastDeliveryStatus.Pending);
            row.Attempts.Should().Be(0, "429 urinish hisoblanmaydi");
            row.NextAttemptAt.Should().BeCloseTo(start.AddSeconds(30), TimeSpan.FromSeconds(1));

            (await Dispatcher.RunOnceAsync()).Claimed.Should().Be(0, "retry_after hali o'tmagan");

            Factory.Clock.Set(start.AddSeconds(31));
            (await Dispatcher.RunOnceAsync()).Sent.Should().Be(1);
        }
        finally
        {
            Factory.Clock.Reset();
        }

        (await GetSummaryAsync(admin, summary.Id)).Status.Should().Be(BroadcastMessageStatus.Completed);
        Telegram.CallsTo(student.TelegramId.Value).Should().HaveCount(2);
    }

    [Fact]
    public async Task Dispatcher_TokenSozlanmagan_Failed()
    {
        await DrainAsync();
        var student = await Factory.CreateStudentAsync();
        Telegram.Script(student.TelegramId!.Value,
            new TelegramSendResult(TelegramSendOutcome.NotConfigured, Error: TelegramMessenger.NotConfiguredError));
        var admin = await Factory.LoginAsAdminAsync();
        var summary = await CreateAsync(admin, "Tokensiz", false, student.Id);

        await Dispatcher.RunOnceAsync();

        var row = (await DeliveriesAsync(summary.Id)).Single();
        row.Status.Should().Be(BroadcastDeliveryStatus.Failed);
        row.Error.Should().Be("Bot tokeni sozlanmagan");
    }

    [Fact]
    public async Task YoqXabar_404()
    {
        var admin = await Factory.LoginAsAdminAsync();
        var id = Guid.NewGuid();
        (await admin.GetAsync($"{Url}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetAsync($"{Url}/{id}/deliveries")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostAsync($"{Url}/{id}/retry", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------------ yordamchilar

    private static string Tag() => $"Xb{Guid.NewGuid():N}"[..12];

    private async Task<MessageSummary> CreateAsync(HttpClient admin, string text, bool button, params Guid[] userIds)
    {
        var response = await admin.PostJsonAsync(Url, new
        {
            text,
            attachAppButton = button,
            audience = new { kind = "selected", userIds }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadAsync<MessageSummary>())!;
    }

    private static async Task<MessageSummary> GetSummaryAsync(HttpClient admin, Guid id)
    {
        var response = await admin.GetAsync($"{Url}/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadAsync<MessageSummary>())!;
    }

    /// <summary>Navbatdagi barcha muddati kelgan yetkazishlarni yuboradi (boshqa testlar qoldirganlari ham).</summary>
    private async Task DrainAsync()
    {
        for (var i = 0; i < 1000; i++)
        {
            if ((await Dispatcher.RunOnceAsync()).Claimed == 0)
                return;
        }
    }

    private Task<List<BroadcastDelivery>> DeliveriesAsync(Guid messageId) =>
        Factory.WithDbAsync(db => db.BroadcastDeliveries.AsNoTracking().Where(d => d.MessageId == messageId).ToListAsync());

    private Task<DateTimeOffset?> BotBlockedAtAsync(Guid userId) =>
        Factory.WithDbAsync(db => db.Users.Where(u => u.Id == userId).Select(u => u.TelegramBotBlockedAt).SingleAsync());

    private Task<string> HemisIdAsync(Guid userId) =>
        Factory.WithDbAsync(db => db.StudentProfiles.Where(p => p.UserId == userId).Select(p => p.HemisId).SingleAsync());

    private static async Task<string?> Detail(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("detail").GetString();
    }
}

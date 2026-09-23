using System.Net;
using System.Net.Http.Json;
using Amaliyotchi.Application.Features.Admin.PracticePeriods;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary><c>/api/admin/practice-periods</c>: CRUD, ustma-ust tushish, yopilgan/faol davr qoidalari, davomat himoyasi.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminPracticePeriodsTests(ApiFixture fixture)
{
    private const string Url = "/api/admin/practice-periods";

    private ApiFactory Factory => fixture.Factory;

    private static string D(DateOnly date) => date.ToString("yyyy-MM-dd");

    private async Task<PracticePeriodDetail> CreateAsync(
        HttpClient client, DateOnly start, DateOnly end, params Guid[] groupIds)
    {
        var response = await client.PostJsonAsync(Url, new
        {
            name = $"Davr {Guid.NewGuid():N}"[..14],
            startDate = D(start),
            endDate = D(end),
            groupIds
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadAsync<PracticePeriodDetail>())!;
    }

    [Fact]
    public async Task Yaratish_201_TafsilotVaRoyxat()
    {
        var groupA = await Factory.CreateGroupAsync(course: 4);
        var groupB = await Factory.CreateGroupAsync();
        await Factory.CreateStudentAsync(group: groupA);
        await Factory.CreateStudentAsync(group: groupA);
        await Factory.CreateStudentAsync(group: groupB);
        var client = await Factory.LoginAsAdminAsync();
        var today = Factory.Today();
        var start = today.AddDays(10);
        var end = today.AddDays(40);

        var response = await client.PostJsonAsync(Url, new
        {
            name = "  Kuzgi amaliyot  ",
            startDate = D(start),
            endDate = D(end),
            groupIds = new[] { groupA.GroupId, groupB.GroupId, groupA.GroupId }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = (await response.Content.ReadAsync<PracticePeriodDetail>())!;
        response.Headers.Location!.ToString().Should().EndWith($"{Url}/{created.Id}");
        created.Name.Should().Be("Kuzgi amaliyot");
        created.StartDate.Should().Be(start);
        created.EndDate.Should().Be(end);
        created.Status.Should().Be(PracticePeriodStatus.Planned);
        created.GroupsCount.Should().Be(2);
        created.StudentsCount.Should().Be(3);
        created.DailyStart.Should().Be("09:00");
        created.DailyEnd.Should().Be("17:00");
        created.WorkDays.Should().NotBeNullOrEmpty();
        created.RequiredDays.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(31);
        var a = created.Groups.Single(g => g.Id == groupA.GroupId);
        a.Code.Should().Be(groupA.GroupName);
        a.Course.Should().Be(4);
        a.StudentsCount.Should().Be(2);
        a.FacultyId.Should().Be(groupA.FacultyId);
        a.DepartmentId.Should().Be(groupA.DepartmentId);
        a.DirectionId.Should().Be(groupA.DirectionId);
        a.DirectionName.Should().NotBeNullOrEmpty();

        // JSON shakli: status camelCase, sanalar YYYY-MM-DD.
        var raw = await (await client.GetAsync($"{Url}/{created.Id}")).Content.ReadAsStringAsync();
        raw.Should().Contain("\"status\":\"planned\"").And.Contain($"\"startDate\":\"{D(start)}\"");

        var detail = await client.GetFromJsonAsync<PracticePeriodDetail>($"{Url}/{created.Id}", JsonDefaults.Options);
        detail!.Groups.Should().HaveCount(2);

        var planned = await client.GetFromJsonAsync<List<PracticePeriodListItem>>($"{Url}?status=planned", JsonDefaults.Options);
        var item = planned!.Single(p => p.Id == created.Id);
        item.GroupsCount.Should().Be(2);
        item.StudentsCount.Should().Be(3);
        planned!.Select(p => p.StartDate).Should().BeInDescendingOrder();

        var active = await client.GetFromJsonAsync<List<PracticePeriodListItem>>($"{Url}?status=active", JsonDefaults.Options);
        active!.Should().NotContain(p => p.Id == created.Id);

        await Factory.WithDbAsync(async db =>
        {
            (await db.AuditLogs.AnyAsync(l => l.Action == AuditAction.PracticePeriodCreated && l.EntityId == created.Id.ToString()))
                .Should().BeTrue();
            // Saqlangan holat — ochiq (Active): mavjud talaba/tyutor oqimlari shunga tayanadi.
            (await db.PracticePeriods.SingleAsync(p => p.Id == created.Id)).Status.Should().Be(PracticePeriodStatus.Active);
        });
    }

    [Fact]
    public async Task Yaratish_Validatsiya_400()
    {
        var group = await Factory.CreateGroupAsync();
        var inactive = await Factory.CreateGroupAsync();
        await Factory.WithDbAsync(async db =>
        {
            (await db.StudentGroups.SingleAsync(g => g.Id == inactive.GroupId)).Deactivate();
            await db.SaveChangesAsync();
        });
        var client = await Factory.LoginAsAdminAsync();
        var today = Factory.Today();

        var noGroups = await client.PostJsonAsync(Url, new { name = "X", startDate = D(today), endDate = D(today.AddDays(5)), groupIds = Array.Empty<Guid>() });
        noGroups.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await noGroups.Content.ReadAsStringAsync()).Should().Contain("GroupIds");

        var reversed = await client.PostJsonAsync(Url, new { name = "X", startDate = D(today.AddDays(5)), endDate = D(today), groupIds = new[] { group.GroupId } });
        reversed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await reversed.Content.ReadAsStringAsync()).Should().Contain("EndDate");

        var blankName = await client.PostJsonAsync(Url, new { name = "  ", startDate = D(today), endDate = D(today), groupIds = new[] { group.GroupId } });
        blankName.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var inactiveGroup = await client.PostJsonAsync(Url, new { name = "X", startDate = D(today), endDate = D(today), groupIds = new[] { inactive.GroupId } });
        inactiveGroup.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await inactiveGroup.Content.ReadAsStringAsync()).Should().Contain(inactive.GroupName);

        var unknownGroup = await client.PostJsonAsync(Url, new { name = "X", startDate = D(today), endDate = D(today), groupIds = new[] { Guid.NewGuid() } });
        unknownGroup.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UstmaUst_Yaratish_Tahrir_Guruhlar_409()
    {
        var shared = await Factory.CreateGroupAsync();
        var other = await Factory.CreateGroupAsync();
        var client = await Factory.LoginAsAdminAsync();
        var today = Factory.Today();
        var first = await CreateAsync(client, today.AddDays(10), today.AddDays(30), shared.GroupId);

        // Yaratish: kesishadi → 409, ro'yxatda guruh va davr nomi.
        var overlap = await client.PostJsonAsync(Url, new
        {
            name = "Ikkinchi", startDate = D(today.AddDays(30)), endDate = D(today.AddDays(50)), groupIds = new[] { shared.GroupId, other.GroupId }
        });
        overlap.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var detail = await overlap.Content.ReadAsStringAsync();
        detail.Should().Contain("Quyidagi guruhlar shu sanalarda boshqa davrga biriktirilgan")
            .And.Contain($"{shared.GroupName} ({first.Name})")
            .And.NotContain(other.GroupName);

        // Kesishmaydi → 201.
        var second = await CreateAsync(client, today.AddDays(31), today.AddDays(50), shared.GroupId);

        // Tahrir: sanalarni birinchisiga suradi → 409.
        var update = await client.PutAsJsonAsync($"{Url}/{second.Id}", new { name = second.Name, startDate = D(today.AddDays(25)), endDate = D(today.AddDays(50)) });
        update.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Guruhlar: kesishadigan davrdagi guruhni qo'shish → 409.
        var third = await CreateAsync(client, today.AddDays(20), today.AddDays(25), other.GroupId);
        var groups = await client.PutAsJsonAsync($"{Url}/{third.Id}/groups", new { groupIds = new[] { other.GroupId, shared.GroupId } });
        groups.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await groups.Content.ReadAsStringAsync()).Should().Contain(shared.GroupName);

        // Yopilgan davr ustma-ust hisoblanmaydi.
        (await client.PostAsync($"{Url}/{first.Id}/close", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var afterClose = await client.PutAsJsonAsync($"{Url}/{third.Id}/groups", new { groupIds = new[] { other.GroupId, shared.GroupId } });
        afterClose.StatusCode.Should().Be(HttpStatusCode.OK, await afterClose.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Tahrir_Rejalashtirilgan_200_RequiredDaysQaytaHisoblanadi()
    {
        var group = await Factory.CreateGroupAsync();
        var client = await Factory.LoginAsAdminAsync();
        var today = Factory.Today();
        var period = await CreateAsync(client, today.AddDays(10), today.AddDays(20), group.GroupId);

        var response = await client.PutAsJsonAsync($"{Url}/{period.Id}", new
        {
            name = "Qayta nomlangan", startDate = D(today.AddDays(12)), endDate = D(today.AddDays(60))
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var updated = (await response.Content.ReadAsync<PracticePeriodDetail>())!;
        updated.Name.Should().Be("Qayta nomlangan");
        updated.StartDate.Should().Be(today.AddDays(12));
        updated.EndDate.Should().Be(today.AddDays(60));
        updated.RequiredDays.Should().BeGreaterThan(period.RequiredDays);
        updated.Groups.Should().ContainSingle(g => g.Id == group.GroupId);

        (await client.PutAsJsonAsync($"{Url}/{Guid.NewGuid()}", new { name = "X", startDate = D(today), endDate = D(today) }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Tahrir_FaolDavr_BoshlanishOzgarsa400_TugashUzaysa200()
    {
        var group = await Factory.CreateGroupAsync();
        var admin = await Factory.CreateAdminAsync();
        var period = await Factory.CreateActivePeriodAsync(group, admin.Id);
        var client = await Factory.LoginAsync(admin);
        var today = Factory.Today();

        var moveStart = await client.PutAsJsonAsync($"{Url}/{period.Id}", new
        {
            name = period.Name, startDate = D(period.StartDate.AddDays(1)), endDate = D(period.EndDate)
        });
        moveStart.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await moveStart.Content.ReadAsStringAsync()).Should().Contain("boshlanish sanasini");

        var endInPast = await client.PutAsJsonAsync($"{Url}/{period.Id}", new
        {
            name = period.Name, startDate = D(period.StartDate), endDate = D(today.AddDays(-1))
        });
        endInPast.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var extend = await client.PutAsJsonAsync($"{Url}/{period.Id}", new
        {
            name = period.Name, startDate = D(period.StartDate), endDate = D(period.EndDate.AddDays(7))
        });
        extend.StatusCode.Should().Be(HttpStatusCode.OK, await extend.Content.ReadAsStringAsync());
        var body = (await extend.Content.ReadAsync<PracticePeriodDetail>())!;
        body.EndDate.Should().Be(period.EndDate.AddDays(7));
        body.Status.Should().Be(PracticePeriodStatus.Active);
    }

    [Fact]
    public async Task Guruhlar_ToliqRoyxat_QoshishVaAjratish_200()
    {
        var keep = await Factory.CreateGroupAsync();
        var drop = await Factory.CreateGroupAsync();
        var add = await Factory.CreateGroupAsync();
        var client = await Factory.LoginAsAdminAsync();
        var today = Factory.Today();
        var period = await CreateAsync(client, today.AddDays(5), today.AddDays(15), keep.GroupId, drop.GroupId);

        var response = await client.PutAsJsonAsync($"{Url}/{period.Id}/groups", new { groupIds = new[] { keep.GroupId, add.GroupId } });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var updated = (await response.Content.ReadAsync<PracticePeriodDetail>())!;
        updated.Groups.Select(g => g.Id).Should().BeEquivalentTo([keep.GroupId, add.GroupId]);
        updated.GroupsCount.Should().Be(2);

        await Factory.WithDbAsync(async db =>
        {
            (await db.PracticePeriodGroups.CountAsync(l => l.PeriodId == period.Id)).Should().Be(2);
            (await db.AuditLogs.AnyAsync(l => l.Action == AuditAction.PracticePeriodGroupsChanged && l.EntityId == period.Id.ToString()))
                .Should().BeTrue();
        });
    }

    [Fact]
    public async Task Guruhlar_DavomatiBorGuruhniAjratish_409()
    {
        var withAttendance = await Factory.CreateGroupAsync();
        var clean = await Factory.CreateGroupAsync();
        var admin = await Factory.CreateAdminAsync();
        var period = await Factory.CreateActivePeriodAsync(withAttendance, admin.Id, clean.GroupId);
        var student = await Factory.CreateStudentAsync(group: withAttendance);
        await Factory.CheckInAsync(student, period, Factory.Today().AddDays(-1));
        var client = await Factory.LoginAsync(admin);

        var response = await client.PutAsJsonAsync($"{Url}/{period.Id}/groups", new { groupIds = new[] { clean.GroupId } });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("davomat yozuvlari bor").And.Contain(withAttendance.GroupName);

        // Davomatsiz guruhni ajratish mumkin.
        var ok = await client.PutAsJsonAsync($"{Url}/{period.Id}/groups", new { groupIds = new[] { withAttendance.GroupId } });
        ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Yopish_200_KeyinTahrirVaGuruh409()
    {
        var group = await Factory.CreateGroupAsync();
        var admin = await Factory.CreateAdminAsync();
        var period = await Factory.CreateActivePeriodAsync(group, admin.Id);
        var client = await Factory.LoginAsync(admin);

        var close = await client.PostAsync($"{Url}/{period.Id}/close", null);

        close.StatusCode.Should().Be(HttpStatusCode.OK);
        (await close.Content.ReadAsync<PracticePeriodDetail>())!.Status.Should().Be(PracticePeriodStatus.Closed);

        var update = await client.PutAsJsonAsync($"{Url}/{period.Id}", new
        {
            name = "Yangi", startDate = D(period.StartDate), endDate = D(period.EndDate)
        });
        update.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var groups = await client.PutAsJsonAsync($"{Url}/{period.Id}/groups", new { groupIds = Array.Empty<Guid>() });
        groups.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await client.PostAsync($"{Url}/{period.Id}/close", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var closed = await client.GetFromJsonAsync<List<PracticePeriodListItem>>($"{Url}?status=closed", JsonDefaults.Options);
        closed!.Should().Contain(p => p.Id == period.Id);
    }

    [Fact]
    public async Task Ochirish_204_KeyinTopilmaydi()
    {
        var group = await Factory.CreateGroupAsync();
        var client = await Factory.LoginAsAdminAsync();
        var today = Factory.Today();
        var period = await CreateAsync(client, today.AddDays(3), today.AddDays(9), group.GroupId);

        var response = await client.DeleteAsync($"{Url}/{period.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"{Url}/{period.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.DeleteAsync($"{Url}/{period.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var all = await client.GetFromJsonAsync<List<PracticePeriodListItem>>(Url, JsonDefaults.Options);
        all!.Should().NotContain(p => p.Id == period.Id);

        // O'chirilgan davr ustma-ust tekshiruvida hisoblanmaydi.
        await CreateAsync(client, today.AddDays(3), today.AddDays(9), group.GroupId);
    }

    [Fact]
    public async Task Ochirish_DavomatiBorDavr_409()
    {
        var group = await Factory.CreateGroupAsync();
        var admin = await Factory.CreateAdminAsync();
        var period = await Factory.CreateActivePeriodAsync(group, admin.Id);
        var student = await Factory.CreateStudentAsync(group: group);
        await Factory.CheckInAsync(student, period, Factory.Today().AddDays(-2));
        var client = await Factory.LoginAsync(admin);

        var response = await client.DeleteAsync($"{Url}/{period.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Yopish");
    }

    [Fact]
    public async Task Auth_AdminBolmagan403_Anonim401()
    {
        var tutor = await Factory.LoginAsTutorAsync();
        var today = Factory.Today();

        (await tutor.GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tutor.PostJsonAsync(Url, new { name = "X", startDate = D(today), endDate = D(today), groupIds = new[] { Guid.NewGuid() } }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tutor.DeleteAsync($"{Url}/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var student = await Factory.CreateStudentAsync();
        var studentClient = await Factory.LoginAsStudentAsync(student);
        (await studentClient.GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await Factory.CreateClient().GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

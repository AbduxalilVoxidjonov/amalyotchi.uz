using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Companies;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary>Korxona check-in QR endpointlari: admin (hamma korxona) va tyutor (ko'lam — <c>GET /api/tutor/companies/{id}</c>
/// dagidek), almashtirish auditi, rol cheklovlari.</summary>
[Collection(ApiCollection.Name)]
public sealed class CompanyCheckInQrEndpointTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    private Task<Company> ReloadAsync(Guid id) =>
        Factory.WithDbAsync(db => db.Companies.AsNoTracking().SingleAsync(c => c.Id == id));

    private Task<List<Domain.Auditing.AuditLog>> RotationAuditsAsync(Guid companyId) =>
        Factory.WithDbAsync(db => db.AuditLogs
            .Where(a => a.Action == AuditAction.CompanyQrRotated && a.EntityId == companyId.ToString())
            .ToListAsync());

    [Fact]
    public async Task Admin_Get_200_KontraktShakli()
    {
        var company = await Factory.CreateCompanyAsync(name: "QR Korxona");
        var admin = await Factory.LoginAsAdminAsync();

        var response = await admin.GetAsync($"/api/admin/companies/{company.Id}/checkin-qr");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        root.GetProperty("companyId").GetGuid().Should().Be(company.Id);
        root.GetProperty("companyName").GetString().Should().Be("QR Korxona");
        root.GetProperty("payload").GetString().Should().Be(company.CheckInQrPayload).And.MatchRegex("^AMLQR:1:[0-9a-f]{32}$");
        root.GetProperty("rotatedAt").GetDateTimeOffset().Should().BeCloseTo(company.CheckInQrRotatedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Admin_YoqKorxona_404()
    {
        var admin = await Factory.LoginAsAdminAsync();

        (await admin.GetAsync($"/api/admin/companies/{Guid.NewGuid()}/checkin-qr")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostAsync($"/api/admin/companies/{Guid.NewGuid()}/checkin-qr/rotate", null)).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_Rotate_200_YangiToken_Audit()
    {
        var company = await Factory.CreateCompanyAsync();
        var admin = await Factory.LoginAsAdminAsync();

        var response = await admin.PostAsync($"/api/admin/companies/{company.Id}/checkin-qr/rotate", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadAsync<CompanyCheckInQrDto>())!;
        dto.CompanyId.Should().Be(company.Id);
        dto.Payload.Should().NotBe(company.CheckInQrPayload).And.MatchRegex("^AMLQR:1:[0-9a-f]{32}$");
        dto.RotatedAt.Should().BeOnOrAfter(company.CheckInQrRotatedAt);

        var reloaded = await ReloadAsync(company.Id);
        reloaded.CheckInQrPayload.Should().Be(dto.Payload);

        var audit = (await RotationAuditsAsync(company.Id)).Should().ContainSingle().Subject;
        audit.UserRole.Should().Be(UserRole.Admin);
        (audit.Changes ?? string.Empty).Should().NotContain(reloaded.CheckInQrToken, "token — sir, auditga yozilmaydi");

        // GET yangi tokenni qaytaradi.
        var get = await (await admin.GetAsync($"/api/admin/companies/{company.Id}/checkin-qr")).Content.ReadAsync<CompanyCheckInQrDto>();
        get!.Payload.Should().Be(dto.Payload);
    }

    [Fact]
    public async Task Tyutor_KolamIchida_200_KolamTashqarisi_404_Rotate_Audit()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var otherGroup = await Factory.CreateGroupAsync();
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id, otherGroup.GroupId);
        var mine = await Factory.CreateStudentAsync(group: group);
        var stranger = await Factory.CreateStudentAsync(group: otherGroup);
        var own = await Factory.CreateCompanyAsync();
        var foreign = await Factory.CreateCompanyAsync();
        var unused = await Factory.CreateCompanyAsync();
        await Factory.CreateApprovedApplicationAsync(mine, period, own, tutor.Id);
        await Factory.CreateApprovedApplicationAsync(stranger, period, foreign, tutor.Id);
        var client = await Factory.LoginAsync(tutor);

        var get = await client.GetAsync($"/api/tutor/companies/{own.Id}/checkin-qr");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        (await get.Content.ReadAsync<CompanyCheckInQrDto>())!.Payload.Should().Be(own.CheckInQrPayload);

        foreach (var id in new[] { foreign.Id, unused.Id, Guid.NewGuid() })
        {
            (await client.GetAsync($"/api/tutor/companies/{id}/checkin-qr")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await client.PostAsync($"/api/tutor/companies/{id}/checkin-qr/rotate", null)).StatusCode
                .Should().Be(HttpStatusCode.NotFound);
        }

        (await ReloadAsync(foreign.Id)).CheckInQrToken.Should().Be(foreign.CheckInQrToken, "ko'lam tashqarisi o'zgarmaydi");
        (await RotationAuditsAsync(foreign.Id)).Should().BeEmpty();

        var rotate = await client.PostAsync($"/api/tutor/companies/{own.Id}/checkin-qr/rotate", null);
        rotate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await rotate.Content.ReadAsync<CompanyCheckInQrDto>())!.Payload.Should().NotBe(own.CheckInQrPayload);

        var audit = (await RotationAuditsAsync(own.Id)).Should().ContainSingle().Subject;
        audit.UserId.Should().Be(tutor.Id);
        audit.UserRole.Should().Be(UserRole.Tutor);
    }

    [Fact]
    public async Task Talaba_403_Tyutor_AdminEndpointiga_403()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var student = await Factory.CreateStudentAsync(group: group);
        var company = await Factory.CreateCompanyAsync();
        var studentClient = await Factory.LoginAsStudentAsync(student);
        var tutorClient = await Factory.LoginAsync(tutor);

        (await studentClient.GetAsync($"/api/admin/companies/{company.Id}/checkin-qr")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await studentClient.PostAsync($"/api/admin/companies/{company.Id}/checkin-qr/rotate", null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await studentClient.GetAsync($"/api/tutor/companies/{company.Id}/checkin-qr")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await studentClient.PostAsync($"/api/tutor/companies/{company.Id}/checkin-qr/rotate", null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        (await tutorClient.GetAsync($"/api/admin/companies/{company.Id}/checkin-qr")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tutorClient.PostAsync($"/api/admin/companies/{company.Id}/checkin-qr/rotate", null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        (await ReloadAsync(company.Id)).CheckInQrToken.Should().Be(company.CheckInQrToken);
    }
}

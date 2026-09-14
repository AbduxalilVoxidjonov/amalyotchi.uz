using System.Net;
using Amaliyotchi.Application.Features.Tutor.LeaveRequests;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Tutor;

[Collection(ApiCollection.Name)]
public sealed class TutorLeaveRequestsTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Royxat_KutilayotganlarBirinchi_HujjatNomBilan()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var today = Factory.Today();
        var decided = await Factory.AddLeaveRequestAsync(s.Student, s.Period, today.AddDays(-5), today.AddDays(-5), attachmentName: null);
        await Factory.WithDbAsync(async db =>
        {
            var l = await db.LeaveRequests.SingleAsync(x => x.Id == decided.Id);
            l.Reject(s.Tutor.Id, "Sabab yetarli emas", Factory.UtcNow());
            await db.SaveChangesAsync();
        });
        var pending = await Factory.AddLeaveRequestAsync(s.Student, s.Period, today.AddDays(2), today.AddDays(3));

        var response = await s.Client.GetAsync("/api/tutor/leave-requests");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.Content.ReadAsync<List<TutorLeaveRequest>>())!;
        list.Select(l => l.Id).Should().ContainInOrder(pending.Id, decided.Id);
        var first = list.Single(l => l.Id == pending.Id);
        first.Status.Should().Be(LeaveRequestStatus.Pending);
        first.StudentName.Should().Be(s.Student.FullName);
        first.Group.Should().Be(s.Group.GroupName);
        first.DateFrom.Should().Be(today.AddDays(2));
        first.DateTo.Should().Be(today.AddDays(3));
        first.Document.Should().NotBeNull();
        first.Document!.Name.Should().Be("spravka.pdf");
        first.Document.Url.Should().BeNull("fayl yuklanmagan — faqat nom");
        var second = list.Single(l => l.Id == decided.Id);
        second.Status.Should().Be(LeaveRequestStatus.Rejected);
        second.Document.Should().BeNull();
        second.Comment.Should().Be("Sabab yetarli emas");

        var onlyPending = (await (await s.Client.GetAsync("/api/tutor/leave-requests?status=pending")).Content.ReadAsync<List<TutorLeaveRequest>>())!;
        onlyPending.Select(l => l.Id).Should().Contain(pending.Id).And.NotContain(decided.Id);
    }

    [Fact]
    public async Task Qaror_Tasdiqlash_IshKunlariSababli_Takror409()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var days = await Factory.PastWorkDaysAsync(s.Period, 2);
        var from = days[1] < days[0] ? days[1] : days[0];
        var to = days[1] < days[0] ? days[0] : days[1];
        await Factory.AddAttendanceAsync(s.Student, s.Period, to); // bu kun allaqachon "keldi" — sababli bo'lib qoladi
        var leave = await Factory.AddLeaveRequestAsync(s.Student, s.Period, from, to);

        var response = await s.Client.PostJsonAsync($"/api/tutor/leave-requests/{leave.Id}/decision", new { decision = "approve", comment = "Tasdiqlandi" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadAsync<TutorLeaveRequest>())!;
        dto.Status.Should().Be(LeaveRequestStatus.Approved);
        dto.Comment.Should().Be("Tasdiqlandi");
        dto.DecidedAt.Should().NotBeNull();

        await Factory.WithDbAsync(async db =>
        {
            var rows = await db.DailyAttendances.AsNoTracking()
                .Where(a => a.StudentUserId == s.Student.Id && a.Date >= from && a.Date <= to)
                .ToListAsync();
            rows.Select(r => r.Date).Should().Contain(days[0]).And.Contain(days[1]);
            rows.Should().OnlyContain(r => r.Status == AttendanceStatus.Excused && r.LeaveRequestId == leave.Id);
            (await db.AuditLogs.AnyAsync(l => l.Action == Amaliyotchi.Domain.Enums.AuditAction.LeaveApproved && l.EntityId == leave.Id.ToString())).Should().BeTrue();
        });

        (await s.Client.PostJsonAsync($"/api/tutor/leave-requests/{leave.Id}/decision", new { decision = "approve" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await s.Client.PostJsonAsync($"/api/tutor/leave-requests/{leave.Id}/decision", new { decision = "reject" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Qaror_RadEtish_Validatsiya_BegonaGuruh404_Talaba403()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var today = Factory.Today();
        var leave = await Factory.AddLeaveRequestAsync(s.Student, s.Period, today.AddDays(1), today.AddDays(1));

        (await s.Client.PostJsonAsync($"/api/tutor/leave-requests/{leave.Id}/decision", new { decision = "maybe" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var strangerTutor = await Factory.LoginAsTutorAsync();
        (await strangerTutor.PostJsonAsync($"/api/tutor/leave-requests/{leave.Id}/decision", new { decision = "reject" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var strangerList = (await (await strangerTutor.GetAsync("/api/tutor/leave-requests")).Content.ReadAsync<List<TutorLeaveRequest>>())!;
        strangerList.Should().NotContain(l => l.Id == leave.Id);

        var studentClient = await Factory.LoginAsStudentAsync(s.Student);
        (await studentClient.PostJsonAsync($"/api/tutor/leave-requests/{leave.Id}/decision", new { decision = "reject" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var rejected = await s.Client.PostJsonAsync($"/api/tutor/leave-requests/{leave.Id}/decision", new { decision = "reject", comment = "Sabab yetarli emas" });
        rejected.StatusCode.Should().Be(HttpStatusCode.OK);
        (await rejected.Content.ReadAsync<TutorLeaveRequest>())!.Status.Should().Be(LeaveRequestStatus.Rejected);
        await Factory.WithDbAsync(async db =>
            (await db.DailyAttendances.AnyAsync(a => a.StudentUserId == s.Student.Id && a.Date == today.AddDays(1))).Should().BeFalse("rad etilganda davomat yozilmaydi"));
    }
}

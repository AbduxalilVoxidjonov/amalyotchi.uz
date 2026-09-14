using System.Net;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Scoping;

/// <summary>Ma'lumot ko'lami: tyutor faqat o'z guruhlaridagi talabalarni ko'radi. HTTP darajasida
/// <c>GET /api/files/{id}</c> orqali (ko'lam <c>IScopeResolver</c> bilan hisoblanadi), DB darajasida <c>ScopeQueries</c> bilan.</summary>
[Collection(ApiCollection.Name)]
public sealed class ScopingTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Tyutor_OzGuruhiTalabasiniKoradi_BegonaGuruhnikiniKormaydi()
    {
        var groupA = await Factory.CreateGroupAsync();
        var groupB = await Factory.CreateGroupAsync();
        var tutorA = await Factory.CreateTutorAsync(groupA);
        var tutorB = await Factory.CreateTutorAsync(groupB);
        var studentA = await Factory.CreateStudentAsync(group: groupA);
        var studentB = await Factory.CreateStudentAsync(group: groupB);

        var fileA = await Factory.CreateStoredFileAsync(studentA.Id);
        var fileB = await Factory.CreateStoredFileAsync(studentB.Id);

        var clientA = await Factory.LoginAsync(tutorA);
        (await clientA.GetAsync($"/api/files/{fileA.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await clientA.GetAsync($"/api/files/{fileB.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "begona guruh — 404, 403 emas");

        var clientB = await Factory.LoginAsync(tutorB);
        (await clientB.GetAsync($"/api/files/{fileB.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await clientB.GetAsync($"/api/files/{fileA.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Talaba_FaqatOzFaylini_AdminHammasini()
    {
        var group = await Factory.CreateGroupAsync();
        var student1 = await Factory.CreateStudentAsync(group: group);
        var student2 = await Factory.CreateStudentAsync(group: group);
        var file1 = await Factory.CreateStoredFileAsync(student1.Id);
        var file2 = await Factory.CreateStoredFileAsync(student2.Id);

        var studentClient = await Factory.LoginAsStudentAsync(student1);
        (await studentClient.GetAsync($"/api/files/{file1.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await studentClient.GetAsync($"/api/files/{file2.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "guruhdoshi bo'lsa ham — faqat o'zi");

        var admin = await Factory.LoginAsAdminAsync();
        (await admin.GetAsync($"/api/files/{file1.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/files/{file2.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Shablon_HammagaOchiq()
    {
        var template = await Factory.CreateStoredFileAsync(null, StoredFileKind.Template, "shartnoma-shablon.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        var student = await Factory.CreateStudentAsync();

        var client = await Factory.LoginAsStudentAsync(student);
        var response = await client.GetAsync($"/api/files/{template.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("shartnoma-shablon.docx");
    }

    [Fact]
    public async Task ScopeQueries_TyutorKolami_FaqatOzGuruhlari()
    {
        var groupA = await Factory.CreateGroupAsync();
        var groupB = await Factory.CreateGroupAsync();
        var tutorA = await Factory.CreateTutorAsync(groupA);
        var studentA = await Factory.CreateStudentAsync(group: groupA);
        var studentB = await Factory.CreateStudentAsync(group: groupB);

        await Factory.WithDbAsync(async db =>
        {
            var groupIds = await db.TutorAssignments
                .Where(a => a.TutorUserId == tutorA.Id && a.IsActive)
                .Select(a => a.StudentGroupId)
                .ToListAsync();
            var studentIds = await db.StudentProfiles
                .Where(p => groupIds.Contains(p.StudentGroupId))
                .Select(p => p.UserId)
                .ToListAsync();
            var scope = DataScope.ForTutor(tutorA.Id, groupIds, studentIds);

            var visible = await db.StudentProfiles.InScope(scope).Select(p => p.UserId).ToListAsync();
            visible.Should().Contain(studentA.Id).And.NotContain(studentB.Id);

            (await db.GetScopedStudentAsync(scope, studentA.Id)).UserId.Should().Be(studentA.Id);
            var act = () => db.GetScopedStudentAsync(scope, studentB.Id);
            await act.Should().ThrowAsync<NotFoundException>();

            (await db.DailyAttendances.InScope(scope).CountAsync()).Should().Be(0, "davomat yo'q, lekin so'rov tarjima qilinadi");
            (await db.StudentProfiles.InScope(DataScope.Empty).AnyAsync()).Should().BeFalse();
            (await db.StudentProfiles.InScope(DataScope.ForStudent(studentB.Id)).Select(p => p.UserId).SingleAsync())
                .Should().Be(studentB.Id);
        });
    }
}

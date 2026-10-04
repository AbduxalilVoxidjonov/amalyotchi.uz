using System.Net;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Scoping;

/// <summary>Ma'lumot ko'lami: admin — hammasi, talaba — faqat o'zi. HTTP darajasida
/// <c>GET /api/files/{id}</c> orqali (ko'lam <c>IScopeResolver</c> bilan hisoblanadi), DB darajasida <c>ScopeQueries</c> bilan.</summary>
[Collection(ApiCollection.Name)]
public sealed class ScopingTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

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
    public async Task ScopeQueries_Talaba_FaqatOzi_Admin_Hammasi_Bosh_Hechnarsa()
    {
        var group = await Factory.CreateGroupAsync();
        var studentA = await Factory.CreateStudentAsync(group: group);
        var studentB = await Factory.CreateStudentAsync(group: group);

        await Factory.WithDbAsync(async db =>
        {
            var self = DataScope.ForStudent(studentA.Id);
            (await db.StudentProfiles.InScope(self).Select(p => p.UserId).SingleAsync()).Should().Be(studentA.Id);
            (await db.GetScopedStudentAsync(self, studentA.Id)).UserId.Should().Be(studentA.Id);
            var act = () => db.GetScopedStudentAsync(self, studentB.Id);
            await act.Should().ThrowAsync<NotFoundException>("ko'lamdan tashqari — 404");

            var admin = DataScope.Unrestricted(Guid.NewGuid());
            var visible = await db.StudentProfiles.InScope(admin).Select(p => p.UserId).ToListAsync();
            visible.Should().Contain([studentA.Id, studentB.Id]);

            (await db.DailyAttendances.InScope(self).CountAsync()).Should().Be(0, "davomat yo'q, lekin so'rov tarjima qilinadi");
            (await db.StudentProfiles.InScope(DataScope.Empty).AnyAsync()).Should().BeFalse();
        });
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Students;
using Amaliyotchi.IntegrationTests.Infrastructure;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary>Talabalarni Excel shabloni orqali import qilish: shablonni yuklab olish va to'ldirilgan
/// faylni qabul qilish (qisman import — xato qatorlar hisobotda, to'g'rilari bazada).</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminStudentImportTests(ApiFixture fixture)
{
    private const string TemplateUrl = "/api/admin/students/import/template";
    private const string ImportUrl = "/api/admin/students/import";

    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Shablon_200_Xlsx_UchtaVaraq_GuruhlarRoyxatiBilan()
    {
        var group = await Factory.CreateGroupAsync();
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.GetAsync(TemplateUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ExcelImport.ContentType);
        response.Content.Headers.ContentDisposition!.FileName.Should().Contain("shablon");

        using var workbook = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        workbook.Worksheets.Select(w => w.Name).Should().BeEquivalentTo("Talabalar", "Yo'riqnoma", "Guruhlar");

        var sheet = workbook.Worksheet("Talabalar");
        sheet.Cell(1, 1).GetString().Should().StartWith(StudentImportColumns.FullName);
        sheet.Cell(1, 2).GetString().Should().StartWith(StudentImportColumns.HemisId);
        sheet.Cell(1, 3).GetString().Should().StartWith(StudentImportColumns.Group);
        sheet.Cell(1, 4).GetString().Should().Be(StudentImportColumns.Phone);
        // Sarlavhadan boshqa qator yo'q — fayl to'ldirishga tayyor.
        sheet.LastRowUsed()!.RowNumber().Should().Be(1);

        var groups = workbook.Worksheet("Guruhlar").RowsUsed().Skip(1).Select(r => r.Cell(1).GetString());
        groups.Should().Contain(group.GroupName);
    }

    [Fact]
    public async Task Import_TogriQatorlarSaqlanadi_XatoQatorlarHisobotda()
    {
        var group = await Factory.CreateGroupAsync();
        var existing = await Factory.CreateStudentAsync(group: group);
        var existingHemis = await Factory.WithDbAsync(db => db.StudentProfiles
            .Where(p => p.UserId == existing.Id).Select(p => p.HemisId).FirstAsync());

        var okHemis = TestClients.RandomHemisId();
        var secondHemis = TestClients.RandomHemisId();
        var phone = TestClients.RandomPhone();

        var file = Workbook(sheet =>
        {
            Header(sheet);
            Row(sheet, 2, "Import Birinchi", okHemis, group.GroupName, phone);
            Row(sheet, 3, "Import Ikkinchi", secondHemis, group.GroupName, null);
            Row(sheet, 4, null, TestClients.RandomHemisId(), group.GroupName, null);
            Row(sheet, 5, "Import Xato Hemis", "abc12", group.GroupName, null);
            Row(sheet, 6, "Import Takror", okHemis, group.GroupName, null);
            Row(sheet, 7, "Import Bazada Bor", existingHemis, group.GroupName, null);
            Row(sheet, 8, "Import Guruhsiz", TestClients.RandomHemisId(), "YO-Q-99", null);
            Row(sheet, 9, "Import Telefon", TestClients.RandomHemisId(), group.GroupName, "123");
        });

        var client = await Factory.LoginAsAdminAsync();
        var response = await client.PostAsync(ImportUrl, ImportForm(file));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadAsync<ImportResult>())!;
        result.TotalRows.Should().Be(8);
        result.Created.Should().Be(2);
        result.Failed.Should().Be(6);

        result.Errors.Should().SatisfyRespectively(
            e => ShouldBeError(e, 4, StudentImportColumns.FullName, StudentImportMessages.FullNameRequiredMessage),
            e => ShouldBeError(e, 5, StudentImportColumns.HemisId, StudentImportMessages.HemisFormatMessage),
            e => ShouldBeError(e, 6, StudentImportColumns.HemisId, StudentImportMessages.HemisDuplicateInFileMessage),
            e => ShouldBeError(e, 7, StudentImportColumns.HemisId, StudentImportMessages.HemisTakenMessage),
            e => ShouldBeError(e, 8, StudentImportColumns.Group, StudentImportMessages.GroupNotFoundMessage),
            e => ShouldBeError(e, 9, StudentImportColumns.Phone, StudentImportMessages.PhoneFormatMessage));

        var saved = await Factory.WithDbAsync(db => db.StudentProfiles
            .Include(p => p.User)
            .Where(p => p.StudentGroupId == group.GroupId && p.HemisId == okHemis)
            .SingleAsync());

        saved.Status.Should().Be(StudentStatus.Active);
        saved.User.FullName.Should().Be("Import Birinchi");
        saved.User.Role.Should().Be(UserRole.Student);
        saved.User.FacultyId.Should().Be(group.FacultyId);
        saved.User.PhoneNumber.Should().Be(phone);
        saved.User.TelegramUserId.Should().BeNull();

        var audited = await Factory.WithDbAsync(db => db.AuditLogs
            .AnyAsync(a => a.Action == AuditAction.StudentsImported));
        audited.Should().BeTrue();

        // Import qilingan talaba darhol ro'yxatda ko'rinadi (Telegramsiz — "Ulanmagan").
        var page = await client.GetPagedAsync<StudentRow>($"/api/admin/students?q={okHemis}");
        page.Items.Single().Status.Should().Be(AdminStudentStatus.Unlinked);
    }

    [Fact]
    public async Task Import_UstunTartibiBoshqa_SarlavhaPastda_RaqamKataklar_Ishlaydi()
    {
        var group = await Factory.CreateGroupAsync();
        var hemisId = TestClients.RandomHemisId();
        var phoneDigits = TestClients.RandomPhone().TrimStart('+');

        var file = Workbook(sheet =>
        {
            // Tepada sarlavha matni, ustunlar boshqa tartibda, HEMIS ID va telefon — RAQAM kataklar.
            sheet.Cell(1, 1).Value = "Talabalar ro'yxati (412-guruh)";
            sheet.Cell(3, 1).Value = "Telefon raqami";
            sheet.Cell(3, 2).Value = "Guruh";
            sheet.Cell(3, 3).Value = "F.I.SH *";
            sheet.Cell(3, 4).Value = "HEMIS ID";
            sheet.Cell(4, 1).Value = double.Parse(phoneDigits, System.Globalization.CultureInfo.InvariantCulture);
            sheet.Cell(4, 2).Value = group.GroupName;
            sheet.Cell(4, 3).Value = "  Import Tartib  ";
            sheet.Cell(4, 4).Value = double.Parse(hemisId, System.Globalization.CultureInfo.InvariantCulture);
            // Orada bo'sh qator — xato emas, tashlab yuboriladi.
            sheet.Cell(6, 3).Value = "Import Tartib Ikki";
            sheet.Cell(6, 4).Value = TestClients.RandomHemisId();
            sheet.Cell(6, 2).Value = group.GroupName;
        }, sheetName: "Ro'yxat");

        var client = await Factory.LoginAsAdminAsync();
        var result = (await (await client.PostAsync(ImportUrl, ImportForm(file)))
            .Content.ReadAsync<ImportResult>())!;

        result.Created.Should().Be(2);
        result.Errors.Should().BeEmpty();

        var saved = await Factory.WithDbAsync(db => db.StudentProfiles
            .Include(p => p.User).Where(p => p.HemisId == hemisId).SingleAsync());
        saved.User.FullName.Should().Be("Import Tartib");
        saved.User.PhoneNumber.Should().Be("+" + phoneDigits);
    }

    [Fact]
    public async Task Import_EskiXlsNomi_400()
    {
        var client = await Factory.LoginAsAdminAsync();
        var file = Workbook(Header);

        var response = await client.PostAsync(ImportUrl, ImportForm(file, "talabalar.xls"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(".xlsx");
    }

    [Fact]
    public async Task Import_BuzuqFayl_400()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostAsync(ImportUrl, ImportForm(Encoding.UTF8.GetBytes("bu excel emas")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(".xlsx");
    }

    [Fact]
    public async Task Import_SarlavhaTopilmasa_400()
    {
        var client = await Factory.LoginAsAdminAsync();
        var file = Workbook(sheet =>
        {
            sheet.Cell(1, 1).Value = "Ism";
            sheet.Cell(1, 2).Value = "Manzil";
            sheet.Cell(2, 1).Value = "Aliyev Akmal";
        });

        var response = await client.PostAsync(ImportUrl, ImportForm(file));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Sarlavha");
    }

    [Fact]
    public async Task Import_Tyutor_403()
    {
        var client = await Factory.LoginAsTutorAsync();

        (await client.GetAsync(TemplateUrl)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync(ImportUrl, ImportForm(Workbook(Header)))).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    /* ── yordamchilar ────────────────────────────────────────────────────── */

    private static void ShouldBeError(ImportError error, int row, string column, string message)
    {
        error.Row.Should().Be(row);
        error.Column.Should().Be(column);
        error.Message.Should().Be(message);
    }

    private static byte[] Workbook(Action<IXLWorksheet> fill, string sheetName = "Talabalar")
    {
        using var workbook = new XLWorkbook();
        fill(workbook.Worksheets.Add(sheetName));
        using var memory = new MemoryStream();
        workbook.SaveAs(memory);
        return memory.ToArray();
    }

    private static void Header(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = $"{StudentImportColumns.FullName} *";
        sheet.Cell(1, 2).Value = $"{StudentImportColumns.HemisId} *";
        sheet.Cell(1, 3).Value = $"{StudentImportColumns.Group} *";
        sheet.Cell(1, 4).Value = StudentImportColumns.Phone;
    }

    private static void Row(IXLWorksheet sheet, int number, string? fullName, string hemisId, string group, string? phone)
    {
        if (fullName is not null)
            sheet.Cell(number, 1).Value = fullName;
        sheet.Cell(number, 2).Value = hemisId;
        sheet.Cell(number, 3).Value = group;
        if (phone is not null)
            sheet.Cell(number, 4).Value = phone;
    }

    private static MultipartFormDataContent ImportForm(byte[] content, string fileName = "talabalar.xlsx")
    {
        var part = new ByteArrayContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue(ExcelImport.ContentType);
        return new MultipartFormDataContent { { part, "file", fileName } };
    }
}

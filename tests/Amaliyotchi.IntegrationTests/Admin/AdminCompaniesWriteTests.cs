using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Companies;
using Amaliyotchi.Application.Features.Companies;
using Amaliyotchi.IntegrationTests.Infrastructure;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary>Korxona yozish amallari: yaratish, tahrirlash, faol holat, arxivlash va Excel importi.
/// Asosiy qoida — korxonani admin OLDINDAN kiritadi, talaba keyin faqat STIR ni yozadi.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminCompaniesWriteTests(ApiFixture fixture)
{
    private const string Url = "/api/admin/companies";

    private ApiFactory Factory => fixture.Factory;

    private static object NewCompany(string tin, string name = "Yangi Korxona MChJ", int? radiusM = 250) => new
    {
        name,
        tin,
        activity = "IT xizmatlari",
        address = "Toshkent sh., Amir Temur 108",
        lat = 41.31105,
        lng = 69.27972,
        radiusM,
        supervisorName = "Karimov Bobur",
        supervisorPhone = "+998901234567",
        mentorName = (string?)null,
        mentorPhone = (string?)null
    };

    private static string RandomTin() => Random.Shared.Next(100_000_000, 999_999_999).ToString();

    [Fact]
    public async Task Yaratish_201_StirBoyichaTopiladi()
    {
        var tin = RandomTin();
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync(Url, NewCompany(tin));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await response.Content.ReadAsync<CompanyDetail>())!;
        created.Tin.Should().Be(tin);
        created.RadiusM.Should().Be(250);
        created.IsActive.Should().BeTrue();
        created.Students.Should().Be(0);

        // Talaba ham shu STIR bo'yicha topa oladi — qo'lda ma'lumot kiritmaydi.
        var student = await Factory.CreateStudentAsync();
        var studentClient = await Factory.LoginAsStudentAsync(student);
        var lookup = await studentClient.GetAsync($"/api/companies/lookup?tin={tin}");

        lookup.StatusCode.Should().Be(HttpStatusCode.OK);
        var found = (await lookup.Content.ReadAsync<CompanyLookupDto>())!;
        found.Id.Should().Be(created.Id);
        found.Address.Should().Be("Toshkent sh., Amir Temur 108");
        found.Lat.Should().BeApproximately(41.31105, 0.00001);
    }

    [Fact]
    public async Task Yaratish_StirTakrorlansa_409()
    {
        var tin = RandomTin();
        var client = await Factory.LoginAsAdminAsync();
        (await client.PostJsonAsync(Url, NewCompany(tin))).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.PostJsonAsync(Url, NewCompany(tin, name: "Ikkinchi"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain(tin);
    }

    [Fact]
    public async Task Yaratish_NotogriStir_400()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync(Url, NewCompany("12345"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("STIR");
    }

    [Fact]
    public async Task Tahrirlash_200_RadiusVaNomYangilanadi()
    {
        var tin = RandomTin();
        var client = await Factory.LoginAsAdminAsync();
        var created = (await (await client.PostJsonAsync(Url, NewCompany(tin))).Content.ReadAsync<CompanyDetail>())!;

        var response = await client.PutAsJsonAsync($"{Url}/{created.Id}", new
        {
            name = "Tahrirlangan MChJ",
            tin,
            activity = "Qurilish",
            address = "Toshkent sh., Chilonzor 5",
            lat = 41.29,
            lng = 69.24,
            radiusM = 400,
            supervisorName = "Yangi Rahbar",
            supervisorPhone = "901234599",
            mentorName = "Mentor Aliyev",
            mentorPhone = (string?)null
        }, JsonDefaults.Options);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await response.Content.ReadAsync<CompanyDetail>())!;
        updated.Name.Should().Be("Tahrirlangan MChJ");
        updated.RadiusM.Should().Be(400);
        updated.SupervisorPhone.Should().Be("+998901234599");
        updated.MentorName.Should().Be("Mentor Aliyev");
    }

    [Fact]
    public async Task Faolsizlantirilgan_Korxona_StirQidiruvida_Chiqmaydi()
    {
        var tin = RandomTin();
        var client = await Factory.LoginAsAdminAsync();
        var created = (await (await client.PostJsonAsync(Url, NewCompany(tin))).Content.ReadAsync<CompanyDetail>())!;

        var patch = await client.PatchAsJsonAsync($"{Url}/{created.Id}/status", new { isActive = false }, JsonDefaults.Options);
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        (await patch.Content.ReadAsync<CompanyDetail>())!.IsActive.Should().BeFalse();

        (await client.GetAsync($"/api/companies/lookup?tin={tin}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        // Qayta faollashtirilsa — yana topiladi.
        await client.PatchAsJsonAsync($"{Url}/{created.Id}/status", new { isActive = true }, JsonDefaults.Options);
        (await client.GetAsync($"/api/companies/lookup?tin={tin}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ochirish_FaolBolsa_409_Faolsizlantirilgach_204()
    {
        var tin = RandomTin();
        var client = await Factory.LoginAsAdminAsync();
        var created = (await (await client.PostJsonAsync(Url, NewCompany(tin))).Content.ReadAsync<CompanyDetail>())!;

        var active = await client.DeleteAsync($"{Url}/{created.Id}");
        active.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await active.Content.ReadAsStringAsync()).Should().Contain("faolsizlantiring");

        await client.PatchAsJsonAsync($"{Url}/{created.Id}/status", new { isActive = false }, JsonDefaults.Options);
        (await client.DeleteAsync($"{Url}/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync($"{Url}/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/companies/lookup?tin={tin}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ochirish_TalabaBiriktirilgan_409()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var company = await Factory.CreateCompanyAsync(name: "Biriktirilgan Korxona");
        var student = await Factory.CreateStudentAsync(group: group);
        await Factory.CreateApprovedApplicationAsync(student, period, company, tutor.Id);

        var client = await Factory.LoginAsAdminAsync();
        await client.PatchAsJsonAsync($"{Url}/{company.Id}/status", new { isActive = false }, JsonDefaults.Options);

        var response = await client.DeleteAsync($"{Url}/{company.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("talaba biriktirilgan");
    }

    [Fact]
    public async Task Import_Shablon_200_VaQismanImport()
    {
        var client = await Factory.LoginAsAdminAsync();

        var template = await client.GetAsync($"{Url}/import/template");
        template.StatusCode.Should().Be(HttpStatusCode.OK);
        template.Content.Headers.ContentType!.MediaType.Should().Be(ExcelImport.ContentType);

        using (var workbook = new XLWorkbook(new MemoryStream(await template.Content.ReadAsByteArrayAsync())))
        {
            workbook.Worksheets.Select(w => w.Name).Should().BeEquivalentTo("Korxonalar", "Yo'riqnoma");
            workbook.Worksheet("Korxonalar").Cell(1, 2).GetString().Should().StartWith(CompanyImportColumns.Tin);
        }

        var okTin = RandomTin();
        var dupTin = RandomTin();
        var file = Workbook(sheet =>
        {
            Header(sheet);
            Row(sheet, 2, "Import Korxona 1", okTin, "41.31105", "69.27972", "250");
            Row(sheet, 3, "Import Korxona 2", dupTin, "41.32", "69.28", null);
            Row(sheet, 4, "Import Korxona 3", dupTin, "41.33", "69.29", null);       // faylda takror
            Row(sheet, 5, "Import Korxona 4", "abc", "41.33", "69.29", null);        // noto'g'ri STIR
            Row(sheet, 6, "Import Korxona 5", RandomTin(), "91.0", "69.29", null);   // noto'g'ri kenglik
            Row(sheet, 7, null, RandomTin(), "41.33", "69.29", null);                // nom yo'q
        });

        var response = await client.PostAsync($"{Url}/import", ImportForm(file));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadAsync<ImportResult>())!;
        result.TotalRows.Should().Be(6);
        result.Created.Should().Be(2);
        result.Failed.Should().Be(4);
        result.Errors.Select(e => e.Row).Should().BeEquivalentTo([4, 5, 6, 7]);

        var saved = await Factory.WithDbAsync(db => db.Companies.AsNoTracking().FirstAsync(c => c.Tin == okTin));
        saved.Name.Should().Be("Import Korxona 1");
        saved.RadiusM.Should().Be(250);
        saved.IsActive.Should().BeTrue();

        // Import qilingan korxona darhol STIR qidiruvida chiqadi.
        (await client.GetAsync($"/api/companies/lookup?tin={okTin}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Lookup_NotogriStir_400_TopilmasaYoqBolsa_404()
    {
        var client = await Factory.LoginAsAdminAsync();

        (await client.GetAsync("/api/companies/lookup?tin=123")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/companies/lookup?tin=999999998")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Yozish_Tyutor_403()
    {
        var client = await Factory.LoginAsTutorAsync();

        (await client.PostJsonAsync(Url, NewCompany(RandomTin()))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"{Url}/import/template")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /* ── yordamchilar ────────────────────────────────────────────────────── */

    private static byte[] Workbook(Action<IXLWorksheet> fill)
    {
        using var workbook = new XLWorkbook();
        fill(workbook.Worksheets.Add("Korxonalar"));
        using var memory = new MemoryStream();
        workbook.SaveAs(memory);
        return memory.ToArray();
    }

    private static void Header(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = $"{CompanyImportColumns.Name} *";
        sheet.Cell(1, 2).Value = $"{CompanyImportColumns.Tin} *";
        sheet.Cell(1, 3).Value = $"{CompanyImportColumns.Activity} *";
        sheet.Cell(1, 4).Value = $"{CompanyImportColumns.Address} *";
        sheet.Cell(1, 5).Value = $"{CompanyImportColumns.Lat} *";
        sheet.Cell(1, 6).Value = $"{CompanyImportColumns.Lng} *";
        sheet.Cell(1, 7).Value = CompanyImportColumns.Radius;
        sheet.Cell(1, 8).Value = $"{CompanyImportColumns.SupervisorName} *";
        sheet.Cell(1, 9).Value = $"{CompanyImportColumns.SupervisorPhone} *";
    }

    private static void Row(IXLWorksheet sheet, int number, string? name, string tin, string lat, string lng, string? radius)
    {
        if (name is not null)
            sheet.Cell(number, 1).Value = name;
        sheet.Cell(number, 2).Value = tin;
        sheet.Cell(number, 3).Value = "IT xizmatlari";
        sheet.Cell(number, 4).Value = "Toshkent sh., Amir Temur 108";
        sheet.Cell(number, 5).Value = lat;
        sheet.Cell(number, 6).Value = lng;
        if (radius is not null)
            sheet.Cell(number, 7).Value = radius;
        sheet.Cell(number, 8).Value = "Karimov Bobur";
        sheet.Cell(number, 9).Value = "+998901234567";
    }

    private static MultipartFormDataContent ImportForm(byte[] content, string fileName = "korxonalar.xlsx")
    {
        var part = new ByteArrayContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue(ExcelImport.ContentType);
        return new MultipartFormDataContent { { part, "file", fileName } };
    }
}

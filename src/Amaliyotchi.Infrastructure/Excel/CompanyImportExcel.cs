using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Companies;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Exceptions;
using ClosedXML.Excel;

namespace Amaliyotchi.Infrastructure.Excel;

/// <summary>Korxonalar importining Excel tomoni (ClosedXML). Talabalar importi bilan bir xil qoida:
/// ustun TARTIBI emas, SARLAVHA NOMLARI o'qiladi (<see cref="ExcelIo"/>).</summary>
internal sealed class CompanyImportExcel : ICompanyImportExcel
{
    public const string DataSheetName = "Korxonalar";
    private const string GuideSheetName = "Yo'riqnoma";

    public const string HeaderNotFoundMessage =
        "Sarlavha qatori topilmadi. Shablondagi «Nomi», «STIR», «Manzil» ustun nomlarini o'zgartirmang.";

    private static readonly (string Field, string[] Aliases)[] HeaderAliases =
    [
        ("name", ["nomi", "korxonanomi", "korxona", "tashkilotnomi", "tashkilot", "nom", "name"]),
        ("tin", ["stir", "stirinn", "inn", "soliqraqami", "tin"]),
        ("activity", ["faoliyatturi", "faoliyat", "activity"]),
        ("address", ["manzil", "manzili", "address"]),
        ("lat", ["kengliklat", "kenglik", "latitude", "lat"]),
        ("lng", ["uzunliklng", "uzunlik", "longitude", "lng", "long"]),
        ("radius", ["radiusm", "radius", "geofenceradius", "radiusmetr"]),
        ("supervisorName", ["rahbarfish", "rahbarism", "rahbar", "supervisorname", "supervisor"]),
        ("supervisorPhone", ["rahbartelefoni", "rahbartelefon", "supervisorphone", "telefon", "telefonraqami"]),
        ("mentorName", ["mentorfish", "mentorism", "mentor", "mentorname"]),
        ("mentorPhone", ["mentortelefoni", "mentortelefon", "mentorphone"])
    ];

    public byte[] BuildTemplate()
    {
        using var workbook = new XLWorkbook();
        BuildDataSheet(workbook);
        BuildGuideSheet(workbook);

        using var memory = new MemoryStream();
        workbook.SaveAs(memory);
        return memory.ToArray();
    }

    public IReadOnlyList<CompanyImportRow> Read(Stream stream, int maxRows)
    {
        using var workbook = ExcelIo.Open(stream);
        var sheet = ExcelIo.Sheet(workbook, DataSheetName);
        var header = ExcelIo.FindHeader(sheet, HeaderAliases, ["name", "tin"], ExcelImport.HeaderSearchRows)
            ?? throw new DomainException(HeaderNotFoundMessage);

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? header.RowNumber;
        var rows = new List<CompanyImportRow>();

        for (var number = header.RowNumber + 1; number <= lastRow; number++)
        {
            var row = sheet.Row(number);
            var values = new CompanyImportRow(
                number,
                ExcelIo.Text(row, header.Column("name")),
                ExcelIo.Text(row, header.Column("tin")),
                ExcelIo.Text(row, header.Column("activity")),
                ExcelIo.Text(row, header.Column("address")),
                ExcelIo.Text(row, header.Column("lat")),
                ExcelIo.Text(row, header.Column("lng")),
                ExcelIo.Text(row, header.Column("radius")),
                ExcelIo.Text(row, header.Column("supervisorName")),
                ExcelIo.Text(row, header.Column("supervisorPhone")),
                ExcelIo.Text(row, header.Column("mentorName")),
                ExcelIo.Text(row, header.Column("mentorPhone")));

            if (IsEmpty(values))
                continue;

            if (rows.Count == maxRows)
                throw new DomainException(ExcelIo.TooManyRowsMessage(maxRows));

            rows.Add(values);
        }

        return rows;
    }

    private static bool IsEmpty(CompanyImportRow row)
        => row.Name is null && row.Tin is null && row.Activity is null && row.Address is null
           && row.Lat is null && row.Lng is null && row.Radius is null
           && row.SupervisorName is null && row.SupervisorPhone is null
           && row.MentorName is null && row.MentorPhone is null;

    /* ── Shablon ─────────────────────────────────────────────────────────── */

    private static readonly (string Header, double Width, bool AsText)[] Columns =
    [
        ($"{CompanyImportColumns.Name} *", 34, false),
        ($"{CompanyImportColumns.Tin} *", 14, true),
        ($"{CompanyImportColumns.Activity} *", 24, false),
        ($"{CompanyImportColumns.Address} *", 40, false),
        ($"{CompanyImportColumns.Lat} *", 16, false),
        ($"{CompanyImportColumns.Lng} *", 16, false),
        (CompanyImportColumns.Radius, 13, false),
        ($"{CompanyImportColumns.SupervisorName} *", 28, false),
        ($"{CompanyImportColumns.SupervisorPhone} *", 18, true),
        (CompanyImportColumns.MentorName, 28, false),
        (CompanyImportColumns.MentorPhone, 18, true)
    ];

    private static void BuildDataSheet(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheets.Add(DataSheetName);

        for (var i = 0; i < Columns.Length; i++)
        {
            var (header, width, asText) = Columns[i];
            var cell = sheet.Cell(1, i + 1);
            cell.Value = header;
            ExcelIo.StyleHeader(cell);

            sheet.Column(i + 1).Width = width;
            // STIR va telefon — MATN formatida: uzun raqam 9,98901E+11 ga aylanib ketmasin.
            if (asText)
                sheet.Column(i + 1).Style.NumberFormat.Format = "@";
        }

        sheet.Row(1).Height = 22;
        sheet.SheetView.FreezeRows(1);
    }

    private static void BuildGuideSheet(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheets.Add(GuideSheetName);
        var row = 1;

        ExcelIo.WriteTitle(sheet.Cell(row++, 1), "Korxonalarni Excel orqali qo'shish");
        row++;

        string[] steps =
        [
            "1. Ma'lumotni «Korxonalar» varag'iga yozing. 1-qator — sarlavha, korxonalar 2-qatordan boshlanadi.",
            "2. Sarlavha nomlarini o'zgartirmang: fayl ustun nomlari bo'yicha o'qiladi (ustunlar tartibi muhim emas).",
            "3. Yulduzcha (*) bilan belgilangan ustunlar majburiy.",
            "4. STIR — 9 ta raqam; bazada va fayl ichida takrorlanmasligi kerak. Talaba keyin faqat shu STIR ni kiritadi.",
            "5. Kenglik/uzunlik — Google Maps yoki Yandex Xaritadan: nuqtani bosing va chiqqan ikkita sondan",
            "   birinchisi kenglik (lat), ikkinchisi uzunlik (lng). Namuna: 41.31105 va 69.27972.",
            $"6. Radius — ixtiyoriy, {Company.MinRadiusM}–{Company.MaxRadiusM} m. Bo'sh qoldirilsa sozlamadagi standart qiymat olinadi.",
            "7. Telefon — namuna: +998901234567 yoki 901234567.",
            "8. Xato qatorlar qabul qilinmaydi — tizim ularni ro'yxat qilib ko'rsatadi, to'g'rilarini saqlaydi.",
            $"9. Bir faylda ko'pi bilan {ExcelImport.MaxRows} ta qator, hajmi {ExcelImport.MaxFileBytes / (1024 * 1024)} MB gacha.",
            "10. Yuklangan korxona darhol faol bo'ladi va STIR qidiruvida chiqadi."
        ];

        foreach (var step in steps)
            sheet.Cell(row++, 1).Value = step;

        row++;
        var sample = sheet.Cell(row++, 1);
        sample.Value = "Namuna (shu qatorni «Korxonalar» varag'iga ko'chirib, o'zingiznikiga almashtiring):";
        sample.Style.Font.Bold = true;

        string[][] sampleRows =
        [
            [.. Columns.Select(c => c.Header)],
            [
                "Tech Solutions MChJ", "305881204", "IT xizmatlari",
                "Toshkent sh., Amir Temur ko'chasi 108", "41.31105", "69.27972", "200",
                "Karimov Bobur Alisherovich", "+998901234567", "Yusupova Nilufar", "901234568"
            ]
        ];

        for (var index = 0; index < sampleRows.Length; index++)
        {
            var sampleRow = sampleRows[index];
            for (var column = 0; column < sampleRow.Length; column++)
            {
                var cell = sheet.Cell(row, column + 1);
                cell.SetValue(sampleRow[column]);
                cell.Style.Font.Bold = index == 0;
            }

            row++;
        }

        sheet.Column(1).Width = 46;
        for (var i = 2; i <= Columns.Length; i++)
            sheet.Column(i).Width = 20;
    }
}

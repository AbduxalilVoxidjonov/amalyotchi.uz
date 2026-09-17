using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Domain.Exceptions;
using ClosedXML.Excel;

namespace Amaliyotchi.Infrastructure.Excel;

/// <summary>Talabalar importining Excel tomoni (ClosedXML): shablon yasash va yuklangan faylni o'qish.
/// Fayl ustun TARTIBI bo'yicha emas, SARLAVHA NOMLARI bo'yicha o'qiladi — shablonni to'ldirgan odam
/// ustunlarni joyini almashtirsa yoki ortiqcha ustun qo'shsa ham ishlaydi.</summary>
internal sealed class StudentImportExcel : IStudentImportExcel
{
    public const string DataSheetName = "Talabalar";
    private const string GuideSheetName = "Yo'riqnoma";
    private const string GroupsSheetName = "Guruhlar";

    public const string HeaderNotFoundMessage =
        "Sarlavha qatori topilmadi. Shablondagi «FISH», «HEMIS ID», «Guruh» ustun nomlarini o'zgartirmang.";

    /// <summary>Sarlavhani tanish uchun nomlar. Har maydon uchun aniqrog'i birinchi: masalan "hemisid"
    /// topilmasa, oxirgi chora sifatida "id" ham qabul qilinadi.</summary>
    private static readonly (string Field, string[] Aliases)[] HeaderAliases =
    [
        ("fullName", ["fish", "fio", "fismsh", "familiyaismsharif", "familiyaismotasiningismi",
                      "talabafish", "talaba", "ismfamiliya", "fullname", "ism"]),
        ("hemisId", ["hemisid", "hemisidraqami", "hemis", "talabaid", "studentid", "id"]),
        ("group", ["guruh", "guruhnomi", "gurux", "group"]),
        ("phone", ["telefon", "telefonraqami", "telraqami", "tel", "phonenumber", "phone"])
    ];

    public byte[] BuildTemplate(IReadOnlyList<StudentImportGroupRef> groups)
    {
        using var workbook = new XLWorkbook();
        BuildDataSheet(workbook);
        BuildGuideSheet(workbook);
        BuildGroupsSheet(workbook, groups);

        using var memory = new MemoryStream();
        workbook.SaveAs(memory);
        return memory.ToArray();
    }

    public IReadOnlyList<StudentImportRow> Read(Stream stream, int maxRows)
    {
        using var workbook = ExcelIo.Open(stream);
        var sheet = ExcelIo.Sheet(workbook, DataSheetName);
        var header = ExcelIo.FindHeader(sheet, HeaderAliases, ["fullName", "hemisId"], ExcelImport.HeaderSearchRows)
            ?? throw new DomainException(HeaderNotFoundMessage);

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? header.RowNumber;
        var rows = new List<StudentImportRow>();

        for (var number = header.RowNumber + 1; number <= lastRow; number++)
        {
            var row = sheet.Row(number);
            var fullName = ExcelIo.Text(row, header.Column("fullName"));
            var hemisId = ExcelIo.Text(row, header.Column("hemisId"));
            var group = ExcelIo.Text(row, header.Column("group"));
            var phone = ExcelIo.Text(row, header.Column("phone"));

            // Butunlay bo'sh qator (orada qoldirilgan bo'shliq) xato hisoblanmaydi.
            if (fullName is null && hemisId is null && group is null && phone is null)
                continue;

            if (rows.Count == maxRows)
                throw new DomainException(ExcelIo.TooManyRowsMessage(maxRows));

            rows.Add(new StudentImportRow(number, fullName, hemisId, group, phone));
        }

        return rows;
    }

    /* ── Shablon ─────────────────────────────────────────────────────────── */

    private static void BuildDataSheet(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheets.Add(DataSheetName);

        string[] headers =
        [
            $"{StudentImportColumns.FullName} *",
            $"{StudentImportColumns.HemisId} *",
            $"{StudentImportColumns.Group} *",
            StudentImportColumns.Phone
        ];

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            ExcelIo.StyleHeader(cell);
        }

        sheet.Column(1).Width = 34;
        sheet.Column(2).Width = 16;
        sheet.Column(3).Width = 14;
        sheet.Column(4).Width = 18;

        // HEMIS ID va telefon — MATN formatida: aks holda Excel uzun raqamni 3,42201E+11 ga aylantiradi
        // va boshidagi nol yo'qoladi.
        sheet.Column(2).Style.NumberFormat.Format = "@";
        sheet.Column(4).Style.NumberFormat.Format = "@";

        sheet.Row(1).Height = 22;
        sheet.SheetView.FreezeRows(1);
    }

    private static void BuildGuideSheet(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheets.Add(GuideSheetName);
        var row = 1;

        ExcelIo.WriteTitle(sheet.Cell(row++, 1), "Talabalarni Excel orqali qo'shish");
        row++;

        string[] steps =
        [
            "1. Ma'lumotni «Talabalar» varag'iga yozing. 1-qator — sarlavha, talabalar 2-qatordan boshlanadi.",
            "2. Sarlavha nomlarini o'zgartirmang: fayl ustun nomlari bo'yicha o'qiladi (ustunlar tartibi muhim emas).",
            "3. Yulduzcha (*) bilan belgilangan ustunlar majburiy: FISH, HEMIS ID, Guruh.",
            "4. HEMIS ID — 5–20 ta raqam; bazada va fayl ichida takrorlanmasligi kerak.",
            "5. Guruh — «Guruhlar» varag'idagi nomlardan biri (masalan 412-22). Kurs guruhdan olinadi, alohida ustun shart emas.",
            "6. Telefon — ixtiyoriy. Namuna: +998901234567 yoki 901234567.",
            "7. Xato qatorlar qabul qilinmaydi — tizim ularni ro'yxat qilib ko'rsatadi, to'g'rilarini saqlaydi.",
            $"8. Bir faylda ko'pi bilan {ExcelImport.MaxRows} ta qator, hajmi {ExcelImport.MaxFileBytes / (1024 * 1024)} MB gacha.",
            "9. Talaba qo'shilgach «Ulanmagan» holatida bo'ladi — u Telegram orqali ulangach «Faol» bo'ladi."
        ];

        foreach (var step in steps)
            sheet.Cell(row++, 1).Value = step;

        row++;
        var sample = sheet.Cell(row++, 1);
        sample.Value = "Namuna (shu qatorlarni «Talabalar» varag'iga ko'chirib, o'zingiznikiga almashtiring):";
        sample.Style.Font.Bold = true;

        string[][] sampleRows =
        [
            [$"{StudentImportColumns.FullName} *", $"{StudentImportColumns.HemisId} *", $"{StudentImportColumns.Group} *", StudentImportColumns.Phone],
            ["Aliyev Akmal Anvarovich", "342201100123", "412-22", "+998901234567"],
            ["Sobirova Madina Baxtiyorovna", "342201100124", "412-22", "901234568"]
        ];

        for (var index = 0; index < sampleRows.Length; index++)
        {
            var sampleRow = sampleRows[index];
            for (var column = 0; column < sampleRow.Length; column++)
            {
                var cell = sheet.Cell(row, column + 1);
                cell.SetValue(sampleRow[column]);
                cell.Style.Font.Bold = index == 0;   // birinchi qator — sarlavha namunasi
            }

            row++;
        }

        sheet.Column(1).Width = 46;
        sheet.Column(2).Width = 18;
        sheet.Column(3).Width = 14;
        sheet.Column(4).Width = 18;
    }

    private static void BuildGroupsSheet(XLWorkbook workbook, IReadOnlyList<StudentImportGroupRef> groups)
    {
        var sheet = workbook.Worksheets.Add(GroupsSheetName);

        string[] headers = ["Guruh", "Kurs", "Yo'nalish", "Kafedra", "Fakultet"];
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            ExcelIo.StyleHeader(cell);
        }

        if (groups.Count == 0)
        {
            sheet.Cell(2, 1).Value = "Faol guruh yo'q — avval fakultet → kafedra → yo'nalish → guruh yarating.";
        }
        else
        {
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var row = i + 2;
                sheet.Cell(row, 1).SetValue(group.Group);
                sheet.Cell(row, 2).Value = group.Course;
                sheet.Cell(row, 3).SetValue(group.Direction);
                sheet.Cell(row, 4).SetValue(group.Department);
                sheet.Cell(row, 5).SetValue(group.Faculty);
            }
        }

        sheet.Column(1).Width = 14;
        sheet.Column(2).Width = 8;
        sheet.Column(3).Width = 34;
        sheet.Column(4).Width = 30;
        sheet.Column(5).Width = 30;
        sheet.Column(1).Style.NumberFormat.Format = "@";
        sheet.SheetView.FreezeRows(1);
    }

}

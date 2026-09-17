using System.Globalization;
using System.Text;
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

    public const string UnreadableMessage =
        "Faylni o'qib bo'lmadi — u haqiqiy .xlsx (Excel) fayli bo'lishi kerak.";
    public const string HeaderNotFoundMessage =
        "Sarlavha qatori topilmadi. Shablondagi «FISH», «HEMIS ID», «Guruh» ustun nomlarini o'zgartirmang.";

    public static string TooManyRowsMessage(int maxRows) =>
        $"Faylda {maxRows} tadan ortiq qator bor — uni bo'lib yuklang.";

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

    private static readonly XLColor HeaderFill = XLColor.FromHtml("#E8EEF7");
    private static readonly XLColor TitleColor = XLColor.FromHtml("#1F3864");

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
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new DomainException(UnreadableMessage);
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets
                            .FirstOrDefault(w => string.Equals(w.Name, DataSheetName, StringComparison.OrdinalIgnoreCase))
                        ?? workbook.Worksheets.FirstOrDefault()
                        ?? throw new DomainException(UnreadableMessage);

            var header = FindHeader(sheet) ?? throw new DomainException(HeaderNotFoundMessage);
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? header.RowNumber;

            var rows = new List<StudentImportRow>();
            for (var number = header.RowNumber + 1; number <= lastRow; number++)
            {
                var row = sheet.Row(number);
                var fullName = Text(row, header.FullName);
                var hemisId = Text(row, header.HemisId);
                var group = Text(row, header.Group);
                var phone = Text(row, header.Phone);

                // Butunlay bo'sh qator (orada qoldirilgan bo'shliq) xato hisoblanmaydi.
                if (fullName is null && hemisId is null && group is null && phone is null)
                    continue;

                if (rows.Count == maxRows)
                    throw new DomainException(TooManyRowsMessage(maxRows));

                rows.Add(new StudentImportRow(number, fullName, hemisId, group, phone));
            }

            return rows;
        }
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
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = HeaderFill;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
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

        var title = sheet.Cell(row++, 1);
        title.Value = "Talabalarni Excel orqali qo'shish";
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 14;
        title.Style.Font.FontColor = TitleColor;
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
            $"8. Bir faylda ko'pi bilan {StudentImportLimits.MaxRows} ta qator, hajmi {StudentImportLimits.MaxFileBytes / (1024 * 1024)} MB gacha.",
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
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = HeaderFill;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
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

    /* ── O'qish ──────────────────────────────────────────────────────────── */

    private sealed record HeaderMap(int RowNumber, int FullName, int HemisId, int? Group, int? Phone);

    /// <summary>Birinchi qatorlar ichidan sarlavhani qidiradi: FISH va HEMIS ID ustunlari topilgan
    /// birinchi qator sarlavha hisoblanadi (tepasida sarlavha matni yoki izoh bo'lishi mumkin).</summary>
    private static HeaderMap? FindHeader(IXLWorksheet sheet)
    {
        foreach (var row in sheet.RowsUsed().Take(StudentImportLimits.HeaderSearchRows))
        {
            var byName = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var cell in row.CellsUsed())
            {
                var key = NormalizeHeader(cell.GetString());
                if (key.Length > 0)
                    byName.TryAdd(key, cell.Address.ColumnNumber);
            }

            if (byName.Count == 0)
                continue;

            var columns = new Dictionary<string, int>(StringComparer.Ordinal);
            var used = new HashSet<int>();
            foreach (var (field, aliases) in HeaderAliases)
            {
                foreach (var alias in aliases)
                {
                    if (!byName.TryGetValue(alias, out var column) || !used.Add(column))
                        continue;
                    columns[field] = column;
                    break;
                }
            }

            if (columns.TryGetValue("fullName", out var fullName) && columns.TryGetValue("hemisId", out var hemisId))
            {
                return new HeaderMap(
                    row.RowNumber(),
                    fullName,
                    hemisId,
                    columns.TryGetValue("group", out var group) ? group : null,
                    columns.TryGetValue("phone", out var phone) ? phone : null);
            }
        }

        return null;
    }

    /// <summary>Sarlavhani solishtirish kaliti: faqat harf va raqamlar qoladi —
    /// "F.I.SH *", "FISH", "fish" bir xil; "HEMIS ID" → "hemisid".</summary>
    private static string NormalizeHeader(string raw)
    {
        var builder = new StringBuilder(raw.Length);
        foreach (var symbol in raw.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(symbol))
                builder.Append(symbol);
        }

        return builder.ToString();
    }

    private static string? Text(IXLRow row, int? column)
        => column is { } number ? CellText(row.Cell(number)) : null;

    /// <summary>Katakni matnga aylantiradi. Raqam sifatida kiritilgan HEMIS ID/telefon
    /// (3,42201E+11) butun son ko'rinishida qaytadi; formula kataklarida keshlangan qiymat olinadi.</summary>
    private static string? CellText(IXLCell cell)
    {
        var value = cell.HasFormula ? cell.CachedValue : cell.Value;

        var text = value switch
        {
            { IsBlank: true } => null,
            { IsError: true } => null,
            { IsNumber: true } => FormatNumber(value.GetNumber()),
            { IsText: true } => value.GetText(),
            { IsDateTime: true } => value.GetDateTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            { IsBoolean: true } => value.GetBoolean() ? "1" : "0",
            _ => cell.GetFormattedString()
        };

        text = text?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static string FormatNumber(double value)
        => value == Math.Floor(value) && Math.Abs(value) < 1e15
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.############", CultureInfo.InvariantCulture);
}

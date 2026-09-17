using System.Globalization;
using System.Text;
using Amaliyotchi.Domain.Exceptions;
using ClosedXML.Excel;

namespace Amaliyotchi.Infrastructure.Excel;

/// <summary>Import shablonlari uchun umumiy ClosedXML mexanikasi: faylni ochish, sarlavha qatorini
/// NOMLAR bo'yicha topish, katakni matnga aylantirish va sarlavha uslubi. Talabalar ham, korxonalar ham
/// shu qoidalarga tayanadi — ustunlar tartibi muhim emas, ortiqcha ustunlar e'tiborsiz qoldiriladi.</summary>
internal static class ExcelIo
{
    public const string UnreadableMessage =
        "Faylni o'qib bo'lmadi — u haqiqiy .xlsx (Excel) fayli bo'lishi kerak.";

    public static readonly XLColor HeaderFill = XLColor.FromHtml("#E8EEF7");
    public static readonly XLColor TitleColor = XLColor.FromHtml("#1F3864");

    public static string TooManyRowsMessage(int maxRows) =>
        $"Faylda {maxRows} tadan ortiq qator bor — uni bo'lib yuklang.";

    /// <summary>Sarlavha qatori: Exceldagi qator raqami va maydon kaliti → ustun raqami.</summary>
    public sealed record HeaderMap(int RowNumber, IReadOnlyDictionary<string, int> Columns)
    {
        public int? Column(string field) => Columns.TryGetValue(field, out var column) ? column : null;
    }

    public static XLWorkbook Open(Stream stream)
    {
        try
        {
            return new XLWorkbook(stream);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new DomainException(UnreadableMessage);
        }
    }

    /// <summary>Nomi bo'yicha varaq; topilmasa — birinchisi.</summary>
    public static IXLWorksheet Sheet(XLWorkbook workbook, string preferredName)
        => workbook.Worksheets.FirstOrDefault(w => string.Equals(w.Name, preferredName, StringComparison.OrdinalIgnoreCase))
           ?? workbook.Worksheets.FirstOrDefault()
           ?? throw new DomainException(UnreadableMessage);

    /// <summary>Birinchi qatorlar ichidan sarlavhani qidiradi: <paramref name="required"/> maydonlarning
    /// hammasi topilgan birinchi qator sarlavha hisoblanadi (tepasida sarlavha matni bo'lishi mumkin).
    /// Har maydon uchun sinonimlar ro'yxati aniqrog'idan boshlab tekshiriladi.</summary>
    public static HeaderMap? FindHeader(
        IXLWorksheet sheet,
        IReadOnlyList<(string Field, string[] Aliases)> fields,
        IReadOnlyCollection<string> required,
        int searchRows)
    {
        foreach (var row in sheet.RowsUsed().Take(searchRows))
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
            foreach (var (field, aliases) in fields)
            {
                foreach (var alias in aliases)
                {
                    if (!byName.TryGetValue(alias, out var column) || !used.Add(column))
                        continue;
                    columns[field] = column;
                    break;
                }
            }

            if (required.All(columns.ContainsKey))
                return new HeaderMap(row.RowNumber(), columns);
        }

        return null;
    }

    /// <summary>Sarlavhani solishtirish kaliti: faqat harf va raqamlar qoladi —
    /// "F.I.SH *", "FISH", "fish" bir xil; "HEMIS ID" → "hemisid".</summary>
    public static string NormalizeHeader(string raw)
    {
        var builder = new StringBuilder(raw.Length);
        foreach (var symbol in raw.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(symbol))
                builder.Append(symbol);
        }

        return builder.ToString();
    }

    public static string? Text(IXLRow row, int? column)
        => column is { } number ? CellText(row.Cell(number)) : null;

    /// <summary>Katakni matnga aylantiradi. Raqam sifatida kiritilgan uzun qiymatlar (HEMIS ID, STIR,
    /// telefon) butun son ko'rinishida qaytadi; formula kataklarida keshlangan qiymat olinadi.</summary>
    public static string? CellText(IXLCell cell)
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

    /// <summary>Kasrli son ham qabul qilinadi (koordinata): "41,3111" → 41.3111.</summary>
    public static bool TryNumber(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var cleaned = text.Trim().Replace(',', '.');
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryInt(string? text, out int value)
    {
        value = 0;
        return TryNumber(text, out var number)
               && number == Math.Floor(number)
               && number is >= int.MinValue and <= int.MaxValue
               && int.TryParse(((long)number).ToString(CultureInfo.InvariantCulture), out value);
    }

    public static void StyleHeader(IXLCell cell)
    {
        cell.Style.Font.Bold = true;
        cell.Style.Fill.BackgroundColor = HeaderFill;
        cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    public static void WriteTitle(IXLCell cell, string text)
    {
        cell.Value = text;
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontSize = 14;
        cell.Style.Font.FontColor = TitleColor;
    }

    private static string FormatNumber(double value)
        => value == Math.Floor(value) && Math.Abs(value) < 1e15
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.############", CultureInfo.InvariantCulture);
}

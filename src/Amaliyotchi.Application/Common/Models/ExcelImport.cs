namespace Amaliyotchi.Application.Common.Models;

/// <summary>Excel (xlsx) importining umumiy chegaralari — talabalar ham, korxonalar ham shu qoidalarga bo'ysunadi.
/// Validator, o'quvchi va hujjat bir xil raqamlarga tayanadi.</summary>
public static class ExcelImport
{
    /// <summary>Bitta faylda ko'pi bilan shuncha ma'lumot qatori.</summary>
    public const int MaxRows = 1000;

    public const long MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>Sarlavha qatori shu qadar birinchi qator ichidan qidiriladi (tepada sarlavha/izoh bo'lishi mumkin).</summary>
    public const int HeaderSearchRows = 20;

    public const string FileExtension = ".xlsx";

    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Qatorga emas, butun faylga tegishli xato uchun ustun nomi.</summary>
    public const string FileColumn = "Fayl";
}

/// <summary>Bitta qabul qilinmagan qator (yoki maydon). Bir qatorda bir nechta xato bo'lsa — bir nechta yozuv.</summary>
public sealed record ImportError(int Row, string Column, string? Value, string Message);

/// <summary>Import hisoboti. Xato qatorlar TASHLAB YUBORILADI, to'g'rilari saqlanadi (qisman import):
/// <paramref name="Created"/> + <paramref name="Failed"/> = <paramref name="TotalRows"/>.</summary>
public sealed record ImportResult(
    int TotalRows, int Created, int Failed, IReadOnlyList<ImportError> Errors);

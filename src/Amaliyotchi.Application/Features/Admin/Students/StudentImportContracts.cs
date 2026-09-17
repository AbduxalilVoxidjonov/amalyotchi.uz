namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary>Excel shablonidagi ustunlar: sarlavha matni (shablon shu nomlar bilan chiqadi) va
/// import hisobotidagi ustun nomi. Import faylni ustun TARTIBI bo'yicha emas, shu NOMLAR bo'yicha
/// o'qiydi — foydalanuvchi ustunlarni joyini almashtirsa ham ishlaydi.</summary>
public static class StudentImportColumns
{
    public const string FullName = "FISH";
    public const string HemisId = "HEMIS ID";
    public const string Group = "Guruh";
    public const string Phone = "Telefon";

    /// <summary>Qatorga emas, butun faylga tegishli xato uchun ustun nomi.</summary>
    public const string File = "Fayl";
}

/// <summary>Import chegaralari — validator, o'quvchi va hujjat bir xil raqamlarga tayanadi.</summary>
public static class StudentImportLimits
{
    /// <summary>Bitta faylda ko'pi bilan shuncha ma'lumot qatori.</summary>
    public const int MaxRows = 1000;

    public const long MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>Sarlavha qatori shu qadar birinchi qator ichidan qidiriladi (tepada sarlavha/izoh bo'lishi mumkin).</summary>
    public const int HeaderSearchRows = 20;

    public const string FileExtension = ".xlsx";

    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Yuklab olinadigan shablon fayl nomi.</summary>
    public const string TemplateFileName = "talabalar-import-shablon.xlsx";
}

/// <summary>Fayldan o'qilgan XOM qator: qiymatlar faqat matnga aylantirilgan (raqam sifatida kiritilgan
/// HEMIS ID eksponent shaklida ketmasligi uchun), tekshirilmagan. <paramref name="RowNumber"/> — Exceldagi
/// qator raqami, hisobotdagi "Qator" ustuni shundan.</summary>
public sealed record StudentImportRow(int RowNumber, string? FullName, string? HemisId, string? Group, string? Phone);

/// <summary>Shablonning "Guruhlar" varag'idagi qator — admin guruh nomini qo'lda yozmasdan shu yerdan ko'chiradi.</summary>
public sealed record StudentImportGroupRef(string Group, int Course, string Direction, string Department, string Faculty);

/// <summary>Bitta qabul qilinmagan qator (yoki maydon). Bir qatorda bir nechta xato bo'lsa — bir nechta yozuv.</summary>
public sealed record StudentImportError(int Row, string Column, string? Value, string Message);

/// <summary>Import hisoboti. Xato qatorlar TASHLAB YUBORILADI, to'g'rilari saqlanadi (qisman import):
/// <paramref name="Created"/> + <paramref name="Failed"/> = <paramref name="TotalRows"/>.</summary>
public sealed record StudentImportResult(
    int TotalRows, int Created, int Failed, IReadOnlyList<StudentImportError> Errors);

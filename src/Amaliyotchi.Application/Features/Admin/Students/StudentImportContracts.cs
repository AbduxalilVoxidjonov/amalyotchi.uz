using Amaliyotchi.Application.Common.Models;

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
    public const string File = ExcelImport.FileColumn;
}

/// <summary>Talabalar importiga xos qiymatlar; umumiy chegaralar — <see cref="ExcelImport"/>.</summary>
public static class StudentImportLimits
{
    /// <summary>Yuklab olinadigan shablon fayl nomi.</summary>
    public const string TemplateFileName = "talabalar-import-shablon.xlsx";
}

/// <summary>Fayldan o'qilgan XOM qator: qiymatlar faqat matnga aylantirilgan (raqam sifatida kiritilgan
/// HEMIS ID eksponent shaklida ketmasligi uchun), tekshirilmagan. <paramref name="RowNumber"/> — Exceldagi
/// qator raqami, hisobotdagi "Qator" ustuni shundan.</summary>
public sealed record StudentImportRow(int RowNumber, string? FullName, string? HemisId, string? Group, string? Phone);

/// <summary>Shablonning "Guruhlar" varag'idagi qator — admin guruh nomini qo'lda yozmasdan shu yerdan ko'chiradi.</summary>
public sealed record StudentImportGroupRef(string Group, int Course, string Direction, string Department, string Faculty);

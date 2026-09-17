using Amaliyotchi.Application.Common.Models;

namespace Amaliyotchi.Application.Features.Admin.Companies;

/// <summary>Excel shablonidagi ustunlar: sarlavha matni va import hisobotidagi ustun nomi.
/// Fayl ustun TARTIBI bo'yicha emas, shu NOMLAR bo'yicha o'qiladi.</summary>
public static class CompanyImportColumns
{
    public const string Name = "Nomi";
    public const string Tin = "STIR";
    public const string Activity = "Faoliyat turi";
    public const string Address = "Manzil";
    public const string Lat = "Kenglik (lat)";
    public const string Lng = "Uzunlik (lng)";
    public const string Radius = "Radius (m)";
    public const string SupervisorName = "Rahbar FISH";
    public const string SupervisorPhone = "Rahbar telefoni";
    public const string MentorName = "Mentor FISH";
    public const string MentorPhone = "Mentor telefoni";

    /// <summary>Qatorga emas, butun faylga tegishli xato uchun ustun nomi.</summary>
    public const string File = ExcelImport.FileColumn;
}

/// <summary>Korxonalar importiga xos qiymatlar; umumiy chegaralar — <see cref="ExcelImport"/>.</summary>
public static class CompanyImportLimits
{
    public const string TemplateFileName = "korxonalar-import-shablon.xlsx";
}

/// <summary>Fayldan o'qilgan XOM qator: qiymatlar faqat matnga aylantirilgan, tekshirilmagan.
/// <paramref name="RowNumber"/> — Exceldagi qator raqami (hisobotdagi "Qator").</summary>
public sealed record CompanyImportRow(
    int RowNumber,
    string? Name,
    string? Tin,
    string? Activity,
    string? Address,
    string? Lat,
    string? Lng,
    string? Radius,
    string? SupervisorName,
    string? SupervisorPhone,
    string? MentorName,
    string? MentorPhone);

/// <summary>Korxonalar importining Excel tomoni. Amalga oshirilishi Infrastructure'da (ClosedXML).</summary>
public interface ICompanyImportExcel
{
    /// <summary>To'ldirish uchun shablon: "Korxonalar" (sarlavha qatori) va "Yo'riqnoma" varaqlari.</summary>
    byte[] BuildTemplate();

    /// <summary>Yuklangan faylni o'qiydi. Fayl buzuq, sarlavha topilmagan yoki qatorlar
    /// <paramref name="maxRows"/> dan ko'p bo'lsa — <see cref="Domain.Exceptions.DomainException"/> (400).</summary>
    IReadOnlyList<CompanyImportRow> Read(Stream stream, int maxRows);
}

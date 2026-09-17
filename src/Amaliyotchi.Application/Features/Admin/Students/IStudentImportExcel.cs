using Amaliyotchi.Application.Common.Models;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary>Excel (xlsx) bilan ishlash porti. Amalga oshirilishi Infrastructure'da (ClosedXML) —
/// Application qatlami fayl formatini bilmaydi. Interfeys shu feature'ning shartnomalariga bog'liq
/// bo'lgani uchun <c>Common/Interfaces</c> da emas, shu papkada turadi.</summary>
public interface IStudentImportExcel
{
    /// <summary>To'ldirish uchun shablon yasaydi: "Talabalar" varag'i (sarlavha qatori), "Yo'riqnoma" va
    /// mavjud faol guruhlar ro'yxati ("Guruhlar").</summary>
    byte[] BuildTemplate(IReadOnlyList<StudentImportGroupRef> groups);

    /// <summary>Yuklangan faylni o'qiydi: sarlavha qatorini nomlar bo'yicha topadi va ma'lumot qatorlarini
    /// xom matn sifatida qaytaradi. Fayl buzuq, sarlavha topilmagan yoki qatorlar
    /// <paramref name="maxRows"/> dan ko'p bo'lsa — <see cref="Domain.Exceptions.DomainException"/> (400).</summary>
    IReadOnlyList<StudentImportRow> Read(Stream stream, int maxRows);
}

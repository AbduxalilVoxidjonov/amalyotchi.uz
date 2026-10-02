using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Hemis = Amaliyotchi.Domain.ValueObjects.HemisId;
using Phone = Amaliyotchi.Domain.ValueObjects.PhoneNumber;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary>Talaba maydonlari qoidalari — Excel import (qator-baqator, <see cref="ImportStudentsCommandHandler"/>) va
/// yakka forma (<see cref="CreateStudentCommandValidator"/>) uchun YAGONA manba. Har metod xato xabarini qaytaradi
/// (<see cref="StudentImportMessages"/>), to'g'ri bo'lsa <c>null</c> va normallashgan qiymat.</summary>
internal static class StudentFieldRules
{
    public const int FullNameMaxLength = 200;

    /// <summary>FISH: bo'shliqlar kesiladi; bo'sh emas, ≤ <see cref="FullNameMaxLength"/> belgi.</summary>
    public static string? FullNameError(string? raw, out string fullName)
    {
        fullName = raw?.Trim() ?? string.Empty;
        if (fullName.Length == 0)
            return StudentImportMessages.FullNameRequiredMessage;
        return fullName.Length > FullNameMaxLength ? StudentImportMessages.FullNameLengthMessage : null;
    }

    /// <summary>HEMIS ID: majburiy, <see cref="Hemis"/> formati (faqat raqam, 5–20 belgi).</summary>
    public static string? HemisIdError(string? raw, out string hemisId)
    {
        hemisId = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return StudentImportMessages.HemisRequiredMessage;
        return Hemis.TryNormalize(raw, out hemisId) ? null : StudentImportMessages.HemisFormatMessage;
    }

    /// <summary>Telefon: ixtiyoriy (bo'sh → <c>null</c>), to'ldirilgan bo'lsa <see cref="Phone"/> ga normallashadi
    /// ("901234567" → "+998901234567").</summary>
    public static string? PhoneError(string? raw, out string? phone)
    {
        phone = null;
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (!Phone.TryNormalize(raw, out var normalized))
            return StudentImportMessages.PhoneFormatMessage;
        phone = normalized;
        return null;
    }
}

/// <summary>Talabani (User role Student + <see cref="StudentProfile"/>) yaratish — import va forma bir xil yo'l bilan.</summary>
internal static class StudentRegistration
{
    /// <summary>Band HEMIS ID'lar — o'chirilmagan profillar (bazadagi unikal indeks <c>is_deleted = false</c> sharti bilan bir xil;
    /// o'chirilgan talabaning HEMIS ID si qayta ishlatilishi mumkin).</summary>
    public static IQueryable<string> TakenHemisIds(IApplicationDbContext db)
        => db.StudentProfiles.AsNoTracking().Select(p => p.HemisId);

    /// <summary>Band telefonlar — o'chirilmagan foydalanuvchilar (unikal indeks sharti bilan bir xil).</summary>
    public static IQueryable<string> TakenPhones(IApplicationDbContext db)
        => db.Users.AsNoTracking().Where(u => u.PhoneNumber != null).Select(u => u.PhoneNumber!);

    /// <summary>Yangi talaba: fakultet — guruh zanjiridan, profil — faol holatda, Telegram bog'lanmagan.
    /// Saqlash (<c>SaveChanges</c>) va audit — chaqiruvchida.</summary>
    public static User Add(IApplicationDbContext db, string fullName, string hemisId, ImportGroup group, string? phone)
    {
        var user = User.CreateStudent(fullName, group.FacultyId, phone);
        db.Users.Add(user);
        db.StudentProfiles.Add(StudentProfile.Create(user.Id, hemisId, group.GroupId));
        return user;
    }

    public static Task<bool> IsHemisTakenAsync(IApplicationDbContext db, string hemisId, CancellationToken cancellationToken)
        => TakenHemisIds(db).AnyAsync(h => h == hemisId, cancellationToken);

    public static Task<bool> IsPhoneTakenAsync(IApplicationDbContext db, string phone, CancellationToken cancellationToken)
        => TakenPhones(db).AnyAsync(p => p == phone, cancellationToken);
}

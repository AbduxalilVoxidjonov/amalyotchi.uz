using System.Globalization;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Tin = Amaliyotchi.Domain.ValueObjects.Tin;

namespace Amaliyotchi.Application.Features.Admin.Companies;

/// <summary>Korxona yozish amallari (create/update/import) uchun umumiy tekshiruvlar.</summary>
internal static class CompanyWrite
{
    public static string TinTakenMessage(string tin) => $"STIR {tin} bilan korxona allaqachon mavjud.";

    /// <summary>STIR band emasligini tekshiradi. O'chirilganlar hisobga olinmaydi (arxivdagi
    /// korxonaning STIR'ini qayta ishlatish mumkin), <paramref name="exceptId"/> — tahrirlanayotgan korxona.</summary>
    public static async Task EnsureTinFreeAsync(
        IApplicationDbContext db, string tin, Guid? exceptId, CancellationToken cancellationToken)
    {
        var taken = await db.Companies
            .AsNoTracking()
            .AnyAsync(c => c.Tin == tin && (exceptId == null || c.Id != exceptId), cancellationToken);

        if (taken)
            throw new ConflictException(TinTakenMessage(tin));
    }

    /// <summary>Radius berilmasa — <c>geofenceRadius</c> sozlamasi, u ham bo'lmasa
    /// <see cref="Company.DefaultRadiusM"/>.</summary>
    public static async Task<int> ResolveRadiusAsync(
        IApplicationDbContext db, int? radiusM, CancellationToken cancellationToken)
    {
        if (radiusM is { } value)
            return value;

        var raw = await db.AppSettings
            .AsNoTracking()
            .Where(s => s.Key == SettingKeys.GeofenceRadius)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        var definition = SettingKeys.Get(SettingKeys.GeofenceRadius);
        return int.TryParse(raw ?? definition.DefaultValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : Company.DefaultRadiusM;
    }

    /// <summary>Korxonaga biriktirilgan (qoralamadan boshqa arizasi bor) talabalar soni —
    /// o'chirish taqiqi shu songa tayanadi.</summary>
    public static Task<int> CountAttachedStudentsAsync(
        IApplicationDbContext db, Guid companyId, CancellationToken cancellationToken)
        => db.PracticeApplications
            .AsNoTracking()
            .Where(a => a.CompanyId == companyId && a.Status != ApplicationStatus.Draft)
            .Select(a => a.StudentUserId)
            .Distinct()
            .CountAsync(cancellationToken);

    /// <summary>STIR'ni normallashtiradi (faqat raqamlar); noto'g'ri bo'lsa <see cref="DomainException"/> (400).</summary>
    public static string NormalizeTin(string tin) => Tin.Normalize(tin);
}

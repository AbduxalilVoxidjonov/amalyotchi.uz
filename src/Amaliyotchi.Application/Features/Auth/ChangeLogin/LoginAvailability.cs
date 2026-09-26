using Amaliyotchi.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Hemis = Amaliyotchi.Domain.ValueObjects.HemisId;

namespace Amaliyotchi.Application.Features.Auth.ChangeLogin;

/// <summary>Login (HEMIS ID) bandligini tekshirish — <c>GET /api/auth/login-available</c> va
/// <c>POST /api/auth/change-login</c> AYNAN shu qoidalardan foydalanadi.</summary>
internal static class LoginAvailability
{
    public const string RequiredMessage = "Loginni kiriting.";
    public const string FormatMessage = "Login faqat raqamlardan iborat, 5–20 belgi bo'lishi kerak (HEMIS ID formati).";
    public const string TakenMessage = "Bu login allaqachon band.";
    public const string OwnLoginMessage = "Bu sizning joriy loginingiz.";

    /// <summary>Format xatosi bo'lsa — xabar, aks holda <c>null</c> (<paramref name="normalized"/> to'ldiriladi).</summary>
    public static string? FormatError(string? raw, out string normalized)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            normalized = string.Empty;
            return RequiredMessage;
        }

        if (!Hemis.TryNormalize(raw, out normalized))
        {
            normalized = raw.Trim();
            return FormatMessage;
        }

        return null;
    }

    /// <summary>Login tizimda kimdadir bormi: xodimlar (<c>users.hemis_id</c>) va talaba profillari
    /// (<c>student_profiles.hemis_id</c>). O'chirilgan (soft delete) yozuvlar ham band hisoblanadi — login identifikatori
    /// qayta ishlatilmaydi (tyutor yaratishdagi qoida bilan bir xil). <paramref name="exceptUserId"/> — o'zini hisobga
    /// olmaslik uchun.</summary>
    public static async Task<bool> IsTakenAsync(
        IApplicationDbContext db, string normalized, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        var byUser = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(u => u.HemisId == normalized && u.Id != exceptUserId, cancellationToken);
        if (byUser)
            return true;

        return await db.StudentProfiles.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(p => p.HemisId == normalized, cancellationToken);
    }
}

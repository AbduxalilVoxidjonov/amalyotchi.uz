namespace Amaliyotchi.Domain.Common;

/// <summary>Audit interceptor'dan istisno qilinadigan entity'lar uchun marker.
/// Yuqori hajmli texnik yozuvlar (check-in urinishlari, fayl metama'lumoti, refresh token)
/// audit jurnalini "shovqin" bilan to'ldirmasligi kerak — ular o'zlari tarix.</summary>
public interface IAuditExempt
{
}

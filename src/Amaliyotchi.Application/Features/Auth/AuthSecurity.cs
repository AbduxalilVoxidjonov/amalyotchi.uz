namespace Amaliyotchi.Application.Features.Auth;

/// <summary>Auth xavfsizlik siyosati qiymatlari (bir joyda; testlar ham shularga tayanadi).</summary>
public static class AuthSecurity
{
    /// <summary>Hisob bo'yicha lockout: <see cref="LockoutWindow"/> ichida shuncha noto'g'ri paroldan keyin hisob
    /// vaqtincha bloklanadi (IP bo'yicha rate limit ko'p IP'li hujumni to'xtatmaydi).</summary>
    public const int LockoutThreshold = 10;

    public static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);

    /// <summary>Bir xil refresh tokenni bir necha so'rov (masalan ikki tab) deyarli bir vaqtda yangilashi — hujum emas.
    /// Shu oynadan keyin almashtirilgan token qayta kelsa — o'g'irlangan deb hisoblanadi.</summary>
    public static readonly TimeSpan RefreshReuseGracePeriod = TimeSpan.FromSeconds(10);

    public const string RefreshReuseRevokeReason = "Almashtirilgan refresh token qayta ishlatildi";
}

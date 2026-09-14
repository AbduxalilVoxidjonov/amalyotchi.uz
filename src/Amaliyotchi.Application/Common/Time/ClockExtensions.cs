using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Common;

namespace Amaliyotchi.Application.Common.Time;

/// <summary><see cref="IClock"/> ni Toshkent vaqtiga bog'laydi. Hisob-kitob Domain'dagi
/// <see cref="PracticeTime"/> da — bu yerda faqat qulaylik (handler'larda <c>clock.LocalToday()</c>).</summary>
public static class ClockExtensions
{
    /// <summary>Hozirgi lahza Toshkent siljishida (+05:00).</summary>
    public static DateTimeOffset LocalNow(this IClock clock) => PracticeTime.ToLocal(clock.UtcNow);

    /// <summary>Bugungi kalendar kun (Toshkent bo'yicha).</summary>
    public static DateOnly LocalToday(this IClock clock) => PracticeTime.LocalDate(clock.UtcNow);

    /// <summary>Hozirgi soat (Toshkent bo'yicha) — oyna tekshiruvlari uchun.</summary>
    public static TimeOnly LocalTime(this IClock clock) => PracticeTime.LocalTime(clock.UtcNow);
}

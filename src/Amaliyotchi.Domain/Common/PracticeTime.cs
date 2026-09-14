namespace Amaliyotchi.Domain.Common;

/// <summary>Amaliyot vaqti — qat'iy Toshkent (+05:00). O'zbekistonda yozgi vaqt yo'q,
/// shuning uchun <c>TimeZoneInfo</c> ishlatilmaydi (InvariantGlobalization ostida ICU xavfi ham yo'q).
/// Bazada hamma narsa UTC; "kun" va "soat" tushunchalari faqat shu yerda hisoblanadi.</summary>
public static class PracticeTime
{
    /// <summary>Toshkent siljishi: UTC+05:00.</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(5);

    /// <summary>Istalgan vaqt momentini Toshkent vaqtiga o'tkazadi.</summary>
    public static DateTimeOffset ToLocal(DateTimeOffset at) => at.ToOffset(Offset);

    /// <summary>Toshkent bo'yicha kalendar kun.</summary>
    public static DateOnly LocalDate(DateTimeOffset at) => DateOnly.FromDateTime(ToLocal(at).DateTime);

    /// <summary>Toshkent bo'yicha kun ichidagi soat.</summary>
    public static TimeOnly LocalTime(DateTimeOffset at) => TimeOnly.FromDateTime(ToLocal(at).DateTime);

    /// <summary>Toshkent kuni + soatini aniq momentga (UTC bilan solishtiriladigan) aylantiradi.</summary>
    public static DateTimeOffset At(DateOnly date, TimeOnly time) => new(date.ToDateTime(time), Offset);

    /// <summary>Kunning boshlanishi (00:00 Toshkent).</summary>
    public static DateTimeOffset StartOfDay(DateOnly date) => At(date, TimeOnly.MinValue);

    /// <summary>API'ga chiqariladigan "HH:mm" ko'rinishi.</summary>
    public static string Hm(TimeOnly time) => time.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Vaqt momentini Toshkent "HH:mm" ko'rinishiga keltiradi.</summary>
    public static string Hm(DateTimeOffset at) => Hm(LocalTime(at));
}

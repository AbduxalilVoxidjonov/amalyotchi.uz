using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Attendance;

/// <summary>Bir kunlik belgilanish qoidalari (Toshkent vaqti). Qiymatlar davr/sozlamalardan keladi:
/// 09:00 dan check-in, 09:15 dan "kech", 10:30 dan yopiq; 17:00 dan check-out, 18:00 da avtomatik yopiladi;
/// GPS aniqligi 100 m dan yomon bo'lsa rad.</summary>
public sealed record CheckInRules
{
    public CheckInRules(
        TimeOnly dailyStart,
        TimeOnly dailyEnd,
        int lateToleranceMinutes,
        int checkInWindowMinutes,
        int checkoutGraceMinutes,
        double minAccuracyM)
    {
        if (dailyEnd <= dailyStart)
            throw new DomainException("Ish tugash vaqti boshlanish vaqtidan keyin bo'lishi kerak.");
        if (lateToleranceMinutes < 0)
            throw new DomainException("Kechikish chegarasi manfiy bo'lishi mumkin emas.");
        if (checkInWindowMinutes <= lateToleranceMinutes)
            throw new DomainException("Check-in oynasi kechikish chegarasidan katta bo'lishi kerak.");
        if (dailyStart.AddMinutes(checkInWindowMinutes) > dailyEnd)
            throw new DomainException("Check-in oynasi ish tugashidan oldin yopilishi kerak.");
        if (checkoutGraceMinutes < 0)
            throw new DomainException("Avtomatik yopish muddati manfiy bo'lishi mumkin emas.");
        if (minAccuracyM <= 0)
            throw new DomainException("Minimal GPS aniqligi musbat bo'lishi kerak.");

        DailyStart = dailyStart;
        DailyEnd = dailyEnd;
        LateToleranceMinutes = lateToleranceMinutes;
        CheckInWindowMinutes = checkInWindowMinutes;
        CheckoutGraceMinutes = checkoutGraceMinutes;
        MinAccuracyM = minAccuracyM;
    }

    /// <summary>Standart qoidalar: 09:00–17:00, 15 daqiqa kechikish, 90 daqiqa oyna, 60 daqiqa avto-yopish, 100 m aniqlik.</summary>
    public static CheckInRules Default { get; } = new(new TimeOnly(9, 0), new TimeOnly(17, 0), 15, 90, 60, 100);

    public TimeOnly DailyStart { get; }
    public TimeOnly DailyEnd { get; }
    public int LateToleranceMinutes { get; }
    public int CheckInWindowMinutes { get; }
    public int CheckoutGraceMinutes { get; }
    public double MinAccuracyM { get; }

    /// <summary>Shu vaqtdan boshlab check-in "kech keldi" (09:15).</summary>
    public TimeOnly LateAfter => DailyStart.AddMinutes(LateToleranceMinutes);

    /// <summary>Shu vaqtdan boshlab check-in qabul qilinmaydi (10:30).</summary>
    public TimeOnly WindowEnd => DailyStart.AddMinutes(CheckInWindowMinutes);

    /// <summary>Check-out ochiladigan vaqt (17:00).</summary>
    public TimeOnly CheckOutFrom => DailyEnd;

    /// <summary>Shu vaqtda check-out qilmaganlar avtomatik yopiladi (18:00). Undan keyin check-out qabul qilinmaydi.</summary>
    public TimeOnly AutoCloseAt => DailyEnd.AddMinutes(CheckoutGraceMinutes);
}

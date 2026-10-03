using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Attendance;

/// <summary>Bir kunlik belgilanish qoidalari (Toshkent vaqti). Qiymatlar davr/sozlamalardan keladi:
/// 09:00 dan check-in, 09:15 dan "kech", 10:30 dan yopiq; 17:00 dan check-out, 18:00 da avtomatik yopiladi;
/// GPS aniqligi 100 m dan yomon bo'lsa rad.
/// Check-in oynasi ish kunidan uzun bo'lsa xato emas — u ish tugashigacha avtomatik qisqaradi
/// (<see cref="EffectiveWindowMinutes"/>); saqlangan daqiqalar o'zgarmaydi, kun uzaysa to'liq oyna qaytadi.</summary>
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

    /// <summary>Amaldagi check-in oynasi (daqiqa): <see cref="CheckInWindowMinutes"/>, lekin ish kunidan
    /// (<see cref="DailyStart"/>–<see cref="DailyEnd"/>) uzun bo'lmaydi — qisqa kunda oyna ish tugashida yopiladi.</summary>
    public int EffectiveWindowMinutes
        => Math.Min(CheckInWindowMinutes, (int)(DailyEnd.ToTimeSpan() - DailyStart.ToTimeSpan()).TotalMinutes);

    /// <summary>Shu vaqtdan boshlab check-in "kech keldi" (09:15). Oyna qisqargan bo'lsa — oyna oxiridan oshmaydi.</summary>
    public TimeOnly LateAfter => DailyStart.AddMinutes(Math.Min(LateToleranceMinutes, EffectiveWindowMinutes));

    /// <summary>Shu vaqtdan boshlab check-in qabul qilinmaydi (10:30); ish tugashidan kech emas.</summary>
    public TimeOnly WindowEnd => DailyStart.AddMinutes(EffectiveWindowMinutes);

    /// <summary>Check-out ochiladigan vaqt (17:00).</summary>
    public TimeOnly CheckOutFrom => DailyEnd;

    /// <summary>Shu vaqtda check-out qilmaganlar avtomatik yopiladi (18:00). Undan keyin check-out qabul qilinmaydi.
    /// Yarim tundan o'tib ketmaydi — kun oxiri (<see cref="TimeOnly.MaxValue"/>) bilan cheklanadi.</summary>
    public TimeOnly AutoCloseAt
        => DailyEnd.ToTimeSpan() + TimeSpan.FromMinutes(CheckoutGraceMinutes) >= TimeSpan.FromDays(1)
            ? TimeOnly.MaxValue
            : DailyEnd.AddMinutes(CheckoutGraceMinutes);

    /// <summary>Boshqa ish vaqti bilan nusxa (talabaning o'z soatlari): daqiqa qoidalari va GPS aniqligi o'zgarmaydi,
    /// oyna yangi kunga moslab qisqaradi. Tugash boshlanishdan oldin bo'lsa → <see cref="DomainException"/>.</summary>
    public CheckInRules WithHours(TimeOnly start, TimeOnly end)
        => new(start, end, LateToleranceMinutes, CheckInWindowMinutes, CheckoutGraceMinutes, MinAccuracyM);
}

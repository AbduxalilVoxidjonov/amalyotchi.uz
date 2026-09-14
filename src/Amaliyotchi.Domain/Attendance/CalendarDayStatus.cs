namespace Amaliyotchi.Domain.Attendance;

/// <summary>Kalendar katagi holati (o'qishda hisoblanadi, bazada saqlanmaydi).</summary>
public enum CalendarDayStatus
{
    Future = 1,
    Pending = 2,
    Present = 3,
    Late = 4,
    Absent = 5,
    Excused = 6,
    DayOff = 7
}

namespace Amaliyotchi.Domain.Attendance;

/// <summary>Kunlik davomat holati. Bazada faqat hodisa bo'lgan kunlar saqlanadi (Present/Late/Excused, qo'lda);
/// Pending/Absent/DayOff o'qishda hisoblanadi. JSON'da camelCase: present, late, dayOff …</summary>
public enum AttendanceStatus
{
    Pending = 1,
    Present = 2,
    Late = 3,
    Absent = 4,
    Excused = 5,
    DayOff = 6
}

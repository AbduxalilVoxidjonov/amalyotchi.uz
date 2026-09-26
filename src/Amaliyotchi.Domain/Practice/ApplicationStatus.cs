namespace Amaliyotchi.Domain.Practice;

/// <summary>Amaliyot arizasi (korxona + shartnoma) holati. JSON'da camelCase: submitted, revisionNeeded …</summary>
public enum ApplicationStatus
{
    Draft = 1,
    Submitted = 2,
    RevisionNeeded = 3,
    Approved = 4,
    Rejected = 5,
    Completed = 6,

    /// <summary>Admin talabani boshqa korxonaga o'tkazgan — ariza yopilgan (JSON: <c>transferred</c>). Tarixiy
    /// davomat/kundalik shu ariza korxonasiga bog'liq qoladi, lekin u hech qayerda "joriy korxona" sifatida tanlanmaydi
    /// va check-in'ga ruxsat bermaydi. Unikal indeks (<c>ix_practice_applications_student_period_active</c>) uni
    /// o'z ichiga olmaydi — bir davrda bitta Transferred + yangi Approved ariza bo'lishi mumkin.</summary>
    Transferred = 7
}

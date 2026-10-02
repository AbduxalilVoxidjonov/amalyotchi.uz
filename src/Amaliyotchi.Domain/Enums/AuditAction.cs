namespace Amaliyotchi.Domain.Enums;

/// <summary>Audit jurnalidagi amal turi. Raqamlar bazada saqlanadi — hech qachon o'zgartirilmaydi,
/// yangilari faqat oxiriga qo'shiladi. 1..6 — texnik (interceptor/auth), 10+ — odam amallari.</summary>
public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
    /// <summary>Odam tomonidan qo'lda aralashuv (davomat tuzatish, qaror bekor qilish).
    /// Har doim sabab bilan yoziladi.</summary>
    ManualOverride = 4,
    LoggedIn = 5,
    LoginFailed = 6,

    /// <summary>Tyutor talaba uchun davomatni qo'lda belgiladi.</summary>
    ManualCheckIn = 10,
    /// <summary>Korxona geofence radiusi o'zgartirildi.</summary>
    RadiusChanged = 11,
    ApplicationApproved = 12,
    ApplicationReturned = 13,
    ApplicationRejected = 14,
    LeaveApproved = 15,
    LeaveRejected = 16,
    DiaryReviewed = 17,
    GradeChanged = 18,
    GradeReverted = 19,
    SettingsChanged = 20,
    AttendanceMarkedSuspicious = 21,

    /// <summary>Admin fakultet yaratdi/tahrirladi/o'chirdi yoki faol holatini o'zgartirdi.</summary>
    FacultyCreated = 22,
    FacultyUpdated = 23,
    FacultyDeleted = 24,
    FacultyActivated = 25,
    FacultyDeactivated = 26,

    /// <summary>Admin kafedra yaratdi/tahrirladi/o'chirdi yoki faol holatini o'zgartirdi.</summary>
    DepartmentCreated = 27,
    DepartmentUpdated = 28,
    DepartmentDeleted = 29,
    DepartmentActivated = 30,
    DepartmentDeactivated = 31,

    /// <summary>Admin yo'nalish yaratdi/tahrirladi/o'chirdi yoki faol holatini o'zgartirdi.</summary>
    DirectionCreated = 32,
    DirectionUpdated = 33,
    DirectionDeleted = 34,
    DirectionActivated = 35,
    DirectionDeactivated = 36,

    /// <summary>Admin guruh yaratdi/tahrirladi/o'chirdi yoki faol holatini o'zgartirdi.</summary>
    GroupCreated = 37,
    GroupUpdated = 38,
    GroupDeleted = 39,
    GroupActivated = 40,
    GroupDeactivated = 41,

    /// <summary>Admin tyutor yaratdi/tahrirladi, faol holatini o'zgartirdi, parolini tikladi
    /// yoki ko'lam (fakultet/kafedra/yo'nalish/guruh) biriktiruvlarini almashtirdi.</summary>
    TutorCreated = 42,
    TutorUpdated = 43,
    TutorActivated = 44,
    TutorDeactivated = 45,
    TutorPasswordReset = 46,
    TutorScopesChanged = 47,

    /// <summary>Admin Excel shabloni orqali talabalarni ommaviy import qildi.</summary>
    StudentsImported = 48,

    /// <summary>Admin korxona yaratdi/tahrirladi, faol holatini o'zgartirdi, arxivladi
    /// yoki Excel shabloni orqali ommaviy yukladi.</summary>
    CompanyCreated = 49,
    CompanyUpdated = 50,
    CompanyActivated = 51,
    CompanyDeactivated = 52,
    CompanyDeleted = 53,
    CompaniesImported = 54,

    /// <summary>Admin tanlangan talabalarni korxonaga biriktirdi (ariza avtomatik tasdiqlanadi).</summary>
    StudentsAssignedToCompany = 55,

    /// <summary>Admin amaliyot davrini yaratdi/tahrirladi, guruhlarini almashtirdi, yopdi yoki o'chirdi.</summary>
    PracticePeriodCreated = 56,
    PracticePeriodUpdated = 57,
    PracticePeriodGroupsChanged = 58,
    PracticePeriodClosed = 59,
    PracticePeriodDeleted = 60,

    /// <summary>Admin yoki tyutor korxonaning check-in QR kodini almashtirdi (eski QR yaroqsiz).</summary>
    CompanyQrRotated = 61,

    /// <summary>Admin yoki tyutor talabaga (brauzer orqali kirish uchun) vaqtinchalik parol o'rnatdi.</summary>
    StudentPasswordSet = 62,

    /// <summary>Foydalanuvchi o'z parolini o'zgartirdi (<c>POST /api/auth/change-password</c>).</summary>
    PasswordChanged = 63,

    /// <summary>Talaba Telegram hisobini o'z hisobiga bog'ladi (<c>POST /api/auth/telegram/link</c>).</summary>
    TelegramLinked = 64,

    /// <summary>Admin talaba profilidan uni korxonaga biriktirdi yoki boshqa korxonaga o'tkazdi
    /// (eski ariza <c>Transferred</c>, yangi korxonaga tasdiqlangan ariza).</summary>
    StudentCompanyReassigned = 65,

    /// <summary>Admin o'z loginini (HEMIS ID) almashtirdi (<c>POST /api/auth/change-login</c>). <c>changes</c> —
    /// <c>{ oldLogin, newLogin }</c>, parol/sirlarsiz.</summary>
    LoginChanged = 66,

    /// <summary>Admin Telegram orqali xabar yubordi ("Xabarlar"). <c>changes</c> — auditoriya, qabul qiluvchilar soni va
    /// matnning qisqartirilgan boshi.</summary>
    BroadcastMessageCreated = 67,

    /// <summary>Admin xabarning muvaffaqiyatsiz (<c>failed</c>) yetkazishlarini qayta navbatga qo'ydi.</summary>
    BroadcastMessageRetried = 68,

    /// <summary>Admin bitta talabani forma orqali qo'shdi (<c>POST /api/admin/students</c>). <c>changes</c> —
    /// <c>{ hemisId, groupId }</c>.</summary>
    StudentCreated = 69
}

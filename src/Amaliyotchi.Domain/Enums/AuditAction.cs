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
    GroupDeactivated = 41
}

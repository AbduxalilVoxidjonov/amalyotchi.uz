/** Auth endpointlari (anonim: telegram, telegram/link, login, refresh; qolganlari Bearer). */
export const AUTH_ENDPOINTS = {
  /** POST /api/auth/telegram { initData } → AuthResultDto | 403 (TelegramLoginCommand). */
  telegram: '/api/auth/telegram',
  /**
   * POST { initData, hemisId, password } → AuthResultDto (+ mustChangePassword) — birinchi kirishda Telegram
   * akkauntini HEMIS ID + parol bilan bog'lash. 403 (parol / faol emas / imzo / xodim) · 409 (boshqa hisobga
   * bog'langan va h.k.) · 429 rate-limit — barchasi ProblemDetails `detail`.
   */
  telegramLink: '/api/auth/telegram/link',
  /** POST { hemisId, password } → AuthResultDto (+ mustChangePassword) | 401/403 `detail` (web-login). */
  login: '/api/auth/login',
  refresh: '/api/auth/refresh',
  /** POST { refreshToken } + Bearer → 204. */
  logout: '/api/auth/logout',
  /** POST { currentPassword, newPassword } + Bearer → 204 | 400 errors.currentPassword/newPassword. */
  changePassword: '/api/auth/change-password',
  me: '/api/auth/me',
} as const;

/**
 * Talaba (TWA) endpointlari — kontrakt v2, manba: `src/Amaliyotchi.Api/Controllers/Student/*.cs`,
 * DTO'lar `Application/Features/Student/StudentContracts.cs` (har feature'ning `types.ts` da aks etgan).
 * Barchasi Bearer (Student) talab qiladi; xatolar — RFC 7807 ProblemDetails, `detail` o'zbekcha.
 */
export const STUDENT_ENDPOINTS = {
  /** GET → TodayDto */
  today: '/api/student/today',
  /** POST multipart(lat,lng,accuracy,occurredAt,photo?) → TodayDto | 400 (oyna/GPS/`errors.Photo`) | 409 (radius, allaqachon) */
  checkin: '/api/student/checkin',
  /** POST multipart(lat,lng,accuracy,occurredAt,photo?) → TodayDto | 400 (oyna/`errors.Photo`) | 409 (check-in yo'q / ketgan / radius) */
  checkout: '/api/student/checkout',
  /**
   * GET → PracticePlaceDto | 404 (joy biriktirilmagan)
   * POST {tin} → PracticePlaceDto (201, status=submitted) | 400 (STIR formati) | 404 (korxona yo'q)
   *            | 409 (faol davr yo'q · ariza ko'rib chiqilmoqda · joy allaqachon biriktirilgan)
   */
  place: '/api/student/place',
  /** GET → DiaryEntryDto[] · POST multipart(text, learned?, files[]) → DiaryEntryDto (201) | 400 | 409 (bugungisi bor) */
  diary: '/api/student/diary',
  /**
   * GET ?periodId=<guid?> → StudentPeriodDays (bosh ekran: davr + har bir kun holati, kundalik holati).
   * `periodId` berilmasa — sukut davr; davr biriktirilmagan → `period: null`, `days: []`.
   */
  periodDays: '/api/student/period-days',
  /** GET → StudentProfileDto (shaxsiy/o'quv ma'lumotlari, amaliyot xulosasi, hisob holati) */
  profile: '/api/student/profile',
  /**
   * PUT { start: "HH:mm"|null, end: "HH:mm"|null } → StudentWorkHoursDto | 400 `errors.start`/`errors.end`.
   * Ikkalasi null — davr vaqtiga qaytarish. O'zgarish ERTADAN kuchga kiradi (bugungi oyna o'zgarmaydi).
   */
  profileWorkHours: '/api/student/profile/work-hours',
  /**
   * Yuzni tasdiqlash (kontrakt v3.27 §6.33):
   * GET → StudentFaceDto · POST multipart(photo, consent="true") → StudentFaceDto (status=pending)
   * | 400 `errors.Photo`/`errors.Consent` (yuz topilmadi, bir nechta yuz, rozilik yo'q) | 409 (allaqachon tasdiqlangan).
   */
  face: '/api/student/face',
} as const;

/**
 * Rolga bog'liq bo'lmagan (har qanday avtorizatsiyalangan foydalanuvchi) endpointlar.
 * Manba: `src/Amaliyotchi.Api/Controllers/CompaniesController.cs`.
 */
export const COMPANY_ENDPOINTS = {
  /** GET ?tin=123456789 → CompanyLookupDto | 400 (STIR formati) | 404 (faol korxona yo'q) */
  lookup: '/api/companies/lookup',
} as const;

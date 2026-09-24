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
  /** GET ?month=YYYY-MM → CalendarMonthDto */
  calendar: '/api/student/calendar',
  /** GET → LeaveRequestDto[] · POST {dateFrom,dateTo,reason,attachmentFileId?} → 201 | 400 | 409 (kesishuvchi) */
  leaveRequests: '/api/student/leave-requests',
  /** GET → PortfolioDto | 404 (faol davr yo'q) */
  portfolio: '/api/student/portfolio',
  /** GET → StudentProfileDto (shaxsiy/o'quv ma'lumotlari, amaliyot xulosasi, hisob holati) */
  profile: '/api/student/profile',
} as const;

/**
 * Rolga bog'liq bo'lmagan (har qanday avtorizatsiyalangan foydalanuvchi) endpointlar.
 * Manba: `src/Amaliyotchi.Api/Controllers/CompaniesController.cs`.
 */
export const COMPANY_ENDPOINTS = {
  /** GET ?tin=123456789 → CompanyLookupDto | 400 (STIR formati) | 404 (faol korxona yo'q) */
  lookup: '/api/companies/lookup',
} as const;

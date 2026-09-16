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
  /** GET → PracticePlaceDto | 404 (joy biriktirilmagan) */
  place: '/api/student/place',
  /** GET → DiaryEntryDto[] · POST multipart(text, learned?, files[]) → DiaryEntryDto (201) | 400 | 409 (bugungisi bor) */
  diary: '/api/student/diary',
  /** GET ?month=YYYY-MM → CalendarMonthDto */
  calendar: '/api/student/calendar',
  /** GET → LeaveRequestDto[] · POST {dateFrom,dateTo,reason,attachmentFileId?} → 201 | 400 | 409 (kesishuvchi) */
  leaveRequests: '/api/student/leave-requests',
  /** GET → PortfolioDto | 404 (faol davr yo'q) */
  portfolio: '/api/student/portfolio',
} as const;

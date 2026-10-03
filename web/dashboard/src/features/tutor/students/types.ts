import type { StatusKind } from '@/shared/ui';
import { APPLICATION_STATUS_LABEL, type ApplicationStatus } from '../applications/types';
import type { DiaryStatus } from '../diaries/types';
import { ATTENDANCE_STATUS_LABEL, SUSPICIOUS_LABEL, type AttendanceStatus } from '../today/types';

/** Backend `StudentState`: davomat < 70% → redFlag; shubhali kunlar bor → suspicious; aks holda active. */
export type StudentState = 'active' | 'redFlag' | 'suspicious';

/** GET /api/tutor/students → TutorStudent[] */
export interface TutorStudent {
  id: string;
  name: string;
  hemisId: string;
  group: string;
  /** Faqat HOZIR aktiv amaliyot o'tayotgan korxona; yo'q bo'lsa (yopilgan davr ham) null → "—". */
  company: string | null;
  /** 0–100 (kasrli bo'lishi mumkin: 84.6). */
  attendancePct: number;
  attendedDays: number;
  totalDays: number;
  diaryCount: number;
  /** O'rtacha ball (4.2); kundalik yo'q bo'lsa 0. */
  diaryAvg: number;
  state: StudentState;
  /** Shubhali kunlar soni ("3 shubhali"). */
  suspiciousCount: number;
}

export const STUDENT_STATE_LABEL: Record<StudentState, { label: string; kind: StatusKind }> = {
  active: { label: 'Faol', kind: 'ok' },
  redFlag: { label: 'Qizil bayroq', kind: 'bad' },
  suspicious: { label: 'Shubhali', kind: 'late' },
};

export function studentStateLabel(s: Pick<TutorStudent, 'state' | 'suspiciousCount'>): {
  label: string;
  kind: StatusKind;
} {
  const base = STUDENT_STATE_LABEL[s.state];
  return s.state === 'suspicious' ? { ...base, label: `${s.suspiciousCount} shubhali` } : base;
}

/* ────────────────────────────────────────────────────────────────────────────
 * Talaba profili (KONTRAKT §2) — GET /api/tutor/students/:id va uning bo'limlari.
 * ──────────────────────────────────────────────────────────────────────────── */

/** Backend `StudentStatus` (mavjud domen enum'i, JSON camelCase). */
export type StudentStatus = 'active' | 'suspended' | 'graduated';

/** Talabaning amaliyot o'tayotgan korxonasi (tasdiqlangan ariza bo'yicha). */
export interface StudentCompany {
  id: string;
  name: string;
  tin: string;
  activity: string;
  address: string;
  supervisorName: string;
  supervisorPhone: string;
  mentorName: string | null;
  mentorPhone: string | null;
  lat: number;
  lng: number;
  /** Geofence radiusi (m). */
  radiusM: number;
}

/**
 * Talaba HOZIR aktiv amaliyot o'tayotgan korxona — tanlangan davrdan mustaqil (ochiq davrdagi
 * tasdiqlangan ariza bo'yicha). Yopilgan/tugagan davrdagi eski korxona bu yerga kirmaydi.
 */
export interface ActiveCompanyRef {
  id: string;
  name: string;
  periodId: string;
  periodName: string;
}

export interface StudentApplicationContract {
  name: string;
  pages: number | null;
  sizeBytes: number;
  /** `/api/files/{id}` */
  url: string;
}

/**
 * Talabaning OXIRGI arizasi holati — tyutor arizalar ro'yxatidagi to'rtta holatdan tashqari
 * domen enum'ida `draft`, `completed` va `transferred` (admin boshqa korxonaga o'tkazgan) ham bor
 * (`ApplicationStatus.cs`).
 */
export type StudentApplicationStatus = ApplicationStatus | 'draft' | 'completed';

export interface StudentApplication {
  id: string;
  status: StudentApplicationStatus;
  /** ISO 8601 */
  submittedAt: string;
  decidedAt: string | null;
  comment: string | null;
  contract: StudentApplicationContract | null;
}

export interface StudentPeriod {
  id: string;
  name: string;
  /** DateOnly */
  startDate: string;
  endDate: string;
  /**
   * "09:00" (Toshkent) — talabaning BUGUN amaldagi ish vaqti: o'zi belgilagan bo'lsa shu, aks holda davrniki.
   */
  dailyStart: string;
  dailyEnd: string;
  /** `true` — `dailyStart`/`dailyEnd` talabaning o'zi belgilagan vaqti. Eski server yubormaydi → `false`. */
  customWorkHours?: boolean;
  /** Ish kunlari: 1 = dushanba … 7 = yakshanba (`WorkDays` flags). */
  workDays: number[];
  requiredDays: number;
}

export interface AttendanceSummary {
  /** Hisobga olinadigan ish kunlari — sababli kunlar bundan chiqarilgan. */
  totalDays: number;
  attendedDays: number;
  lateDays: number;
  excusedDays: number;
  absentDays: number;
  suspiciousDays: number;
  /** 0–100, 1 kasr. */
  attendancePct: number;
}

export interface DiarySummary {
  count: number;
  scoredCount: number;
  avg: number;
}

export interface StudentGrade {
  total: number;
  grade: 2 | 3 | 4 | 5 | null;
}

/** GET /api/tutor/students/:id → TutorStudentDetail (ko'lamdan tashqari → 404). */
export interface TutorStudentDetail {
  id: string;
  name: string;
  hemisId: string;
  group: string;
  course: number;
  faculty: string;
  direction: string;
  status: StudentStatus;
  phone: string | null;
  state: StudentState;
  suspiciousCount: number;
  /**
   * TANLANGAN davrdagi korxona (tarix) — faqat tasdiqlangan (approved/completed) arizadan keladi;
   * aks holda null. "Joriy korxona" uchun `activeCompany` ishlatiladi.
   */
  company: StudentCompany | null;
  /** Hozirda aktiv amaliyot o'tayotgan korxona (davrdan mustaqil); yo'q bo'lsa null. */
  activeCompany: ActiveCompanyRef | null;
  application: StudentApplication | null;
  period: StudentPeriod | null;
  attendance: AttendanceSummary;
  diary: DiarySummary;
  grade: StudentGrade | null;
  /** v3.5: davr tanlagichi — `startDate` kamayish tartibida (guruh davrlari ∪ talaba yozuvlari bor davrlar). */
  periods: StudentPeriodOption[];
  /** Javobdagi davrga bog'liq bloklar shu davr bo'yicha; davr yo'q → null. */
  selectedPeriodId: string | null;
  /** Talabaga parol o'rnatilganmi (TWA'ga brauzerda HEMIS ID + parol bilan kirish). Admin javobida ham bor. */
  hasPassword: boolean;
}

/**
 * v3.5 (§4.6) — talaba davrlaridan biri. `status` hisoblangan: yopilgan → `closed`,
 * boshlanmagan → `planned`, aks holda `active` (tugagan, lekin yopilmagan davr ham `active` —
 * "Tugagan" frontendda `endDate < bugun` dan chiqariladi, § `periods.ts`).
 */
export interface StudentPeriodOption {
  id: string;
  name: string;
  /** DateOnly */
  startDate: string;
  endDate: string;
  status: 'planned' | 'active' | 'closed';
  /** `periodId` berilmaganda backend tanlaydigan davr. */
  isDefault: boolean;
}

/**
 * Bitta belgilanish (check-in yoki check-out) — FAQAT QABUL QILINGAN urinishdan quriladi.
 * Rad etilgan urinishlar haqida faqat `attempts` / `rejectedAttempts` sonlari bo'ladi
 * (rad etilgan nuqtaning koordinatasi/masofasi bu yerda ko'rinmaydi).
 */
export interface AttendancePunch {
  /** "09:02" (Toshkent) */
  at: string;
  /** To'liq ISO 8601 */
  atIso: string;
  distanceM: number | null;
  accuracyM: number | null;
  /** `AttendanceEvent.Location` dan; bo'lmasa null. */
  lat: number | null;
  lng: number | null;
  /** `/api/files/{id}` — check-in selfie; bo'lmasa null. */
  photoUrl: string | null;
  /** `distanceM > company.radiusM` */
  outOfRadius: boolean;
}

/**
 * Rad etish sababi (backend `CheckInRejectReason`, camelCase — KONTRAKT §3.1). Ro'yxat ochiq:
 * backend yangi sabab qo'shsa ham ishlaydi (yorliq bo'lmasa `rejectMessage` yoki umumiy matn).
 */
export type AttendanceRejectReason =
  | 'notApproved'
  | 'notWorkDay'
  | 'periodNotStarted'
  | 'periodEnded'
  | 'windowNotOpen'
  | 'windowClosed'
  | 'poorAccuracy'
  | 'outOfRadius'
  | 'alreadyCheckedIn'
  | 'noCheckIn'
  | 'alreadyCheckedOut'
  | 'onLeave'
  | 'qrInvalid'
  | (string & {});

/** Zaxira yorliqlar (KONTRAKT §3.2 matnlari) — `rejectMessage` bo'lmaganda ishlatiladi. */
export const REJECT_REASON_LABEL: Record<string, string> = {
  notApproved: 'Amaliyot joyi hali tasdiqlanmagan',
  notWorkDay: 'Ish kuni emas',
  periodNotStarted: 'Amaliyot davri hali boshlanmagan',
  periodEnded: 'Amaliyot davri tugagan',
  windowNotOpen: 'Belgilanish oynasi hali ochilmagan',
  windowClosed: 'Belgilanish oynasi yopilgan',
  poorAccuracy: 'GPS aniqligi yetarli emas',
  outOfRadius: 'Amaliyot joyida emas (radius tashqarisi)',
  alreadyCheckedIn: 'Allaqachon belgilangan',
  noCheckIn: 'Kirish belgilanmagan',
  alreadyCheckedOut: 'Ketish allaqachon belgilangan',
  onLeave: 'Bu kunga ruxsat tasdiqlangan',
  qrInvalid: 'QR kod mos emas',
};

export function rejectReasonText(
  attempt: Pick<AttendanceAttempt, 'rejectReason' | 'rejectMessage'>,
): string {
  if (attempt.rejectMessage) return attempt.rejectMessage;
  if (attempt.rejectReason) return REJECT_REASON_LABEL[attempt.rejectReason] ?? 'Rad etildi';
  return 'Rad etildi';
}

/**
 * Kundagi HAR BIR urinish (qabul qilingan ham, rad etilgan ham) — `StudentAttendanceDay.events`.
 * Vaqt tartibida keladi. `photoUrl` — `/api/files/{id}` (AuthImage bilan ochiladi).
 */
export interface AttendanceAttempt {
  id: string;
  kind: 'checkIn' | 'checkOut';
  /** "09:02" (Toshkent) */
  at: string;
  atIso: string;
  accepted: boolean;
  rejectReason: AttendanceRejectReason | null;
  /** Talabaga ko'rsatilgan xabar (o'zbekcha). */
  rejectMessage: string | null;
  distanceM: number | null;
  accuracyM: number | null;
  /** Urinish paytidagi korxona radiusi. */
  radiusM: number | null;
  lat: number | null;
  lng: number | null;
  photoUrl: string | null;
}

export interface AttendanceDayDiary {
  id: string;
  status: DiaryStatus;
  score: number | null;
}

/** GET /api/tutor/students/:id/attendance?from=&to= → StudentAttendanceDay[] */
export interface StudentAttendanceDay {
  /** DateOnly "2026-10-12" */
  date: string;
  status: AttendanceStatus;
  isWorkDay: boolean;
  checkIn: AttendancePunch | null;
  checkOut: AttendancePunch | null;
  /** Check-out bo'lmagani uchun tizim yopgan. */
  autoClosed: boolean;
  suspicious: boolean;
  suspiciousReason: string | null;
  /** Tyutor qo'lda belgilagan. */
  manual: boolean;
  manualReason: string | null;
  leaveRequestId: string | null;
  diary: AttendanceDayDiary | null;
  /** Shu kundagi check-in urinishlari (qabul qilingan + rad etilgan). */
  attempts: number;
  /** Ulardan rad etilganlari — masofasi/nuqtasi `checkIn` da ko'rinmaydi. */
  rejectedAttempts: number;
  /**
   * Shu kundagi barcha urinishlar (kirish + chiqish, qabul qilingan + rad etilgan), vaqt tartibida.
   * Eski backend javobida bo'lmasa `studentsApi.attendance` uni `[]` ga to'ldiradi.
   */
  events: AttendanceAttempt[];
}

/**
 * Davomat va kundalik bo'limlari ikkala profilda ham ishlatiladi: tyutorniki
 * (`/api/tutor/students/...`) va adminniki (`/api/admin/students/...`) — shakl bir xil,
 * faqat yo'l va ruxsat farq qiladi.
 */
export type StudentApiArea = 'tutor' | 'admin';

/** Davomat so'rovi oralig'i (ikkalasi ham null → butun davr). */
export interface AttendanceRange {
  from: string | null;
  to: string | null;
}

export const STUDENT_STATUS_LABEL: Record<StudentStatus, { label: string; kind: StatusKind }> = {
  active: { label: "O'qimoqda", kind: 'ok' },
  // Domen izohi: akademik ta'til yoki vaqtincha chetlashtirilgan — amaliyotga chiqmaydi.
  suspended: { label: "To'xtatilgan", kind: 'late' },
  graduated: { label: 'Bitirgan', kind: 'info' },
};

/** Ariza holati yorliqlari — tyutor ro'yxatidagilar (`transferred` ham) + `draft`/`completed`. */
export const STUDENT_APPLICATION_STATUS_LABEL: Record<
  StudentApplicationStatus,
  { label: string; kind: StatusKind }
> = {
  ...APPLICATION_STATUS_LABEL,
  draft: { label: 'Qoralama', kind: 'neu' },
  completed: { label: 'Yakunlangan', kind: 'ok' },
};

/** Korxona faqat shu holatlardagi arizadan keladi (backend `GetTutorStudentDetailQuery`). */
export function isCompanyBoundApplication(status: StudentApplicationStatus): boolean {
  return status === 'approved' || status === 'completed';
}

const WEEK_DAYS_UZ = ['Du', 'Se', 'Ch', 'Pa', 'Ju', 'Sh', 'Ya'] as const;

/** `[1,2,3,4,5,6]` → "Du, Se, Ch, Pa, Ju, Sh". */
export function workDaysLabel(days: readonly number[]): string {
  const names = days
    .filter((d) => d >= 1 && d <= 7)
    .map((d) => WEEK_DAYS_UZ[d - 1])
    .filter((n): n is (typeof WEEK_DAYS_UZ)[number] => Boolean(n));
  return names.length > 0 ? names.join(', ') : '—';
}

/** Kun qatori uchun badge: shubhali/radius tashqarisi → "Shubhali", aks holda holat. */
export function dayStatusLabel(day: StudentAttendanceDay): { label: string; kind: StatusKind } {
  if (day.suspicious || day.checkIn?.outOfRadius) return SUSPICIOUS_LABEL;
  return ATTENDANCE_STATUS_LABEL[day.status];
}

/** "41.3111, 69.2797" (xarita placeholder va lokatsiya katagi uchun). */
export function fmtCoords(lat: number | null, lng: number | null): string | null {
  if (lat === null || lng === null) return null;
  return `${lat.toFixed(5)}, ${lng.toFixed(5)}`;
}

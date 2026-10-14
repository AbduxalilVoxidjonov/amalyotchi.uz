import type { StatusKind } from '@/shared/ui';

/** `?status=` filtri (backend `TodayFilter`): enum holatlari + `suspicious` (bayroq bo'yicha). */
export type AttendanceFilter =
  'all' | 'present' | 'late' | 'absent' | 'excused' | 'pending' | 'suspicious';

/** Kunlik davomat holati (backend `AttendanceStatus`, JSON camelCase). */
export type AttendanceStatus = 'pending' | 'present' | 'late' | 'absent' | 'excused' | 'dayOff';

/** Bugungi kundalik holati (backend `DiaryState`; null — ish kuni emas / kelmagan). */
export type DiaryState = 'written' | 'pending' | null;

export type TodayAlertKind = 'outOfRadius' | 'notCheckedIn' | 'newApplications';

export interface TodayStats {
  present: number;
  late: number;
  absent: number;
  excused: number;
  pending: number;
  /** Bugun yozilgan kundaliklar. */
  diaries: number;
  total: number;
}

export interface TodayAlert {
  kind: TodayAlertKind;
  count: number;
  /** Ichki havola (masalan `/admin/applications`). */
  href: string;
  /** Faqat `outOfRadius` — bugungi eng uzoq rad etilgan urinish (m). */
  maxDistanceM?: number | null;
}

export interface AttendanceRow {
  studentId: string;
  name: string;
  group: string;
  /** Tasdiqlangan ariza bo'lmasa null → "—". */
  company: string | null;
  /** "09:02" (Toshkent) | null → "—" */
  checkIn: string | null;
  checkOut: string | null;
  diary: DiaryState;
  /** metr; null → "—" */
  distanceM: number | null;
  outOfRadius: boolean;
  status: AttendanceStatus;
  /** Shubhali kun (GPS/vaqt anomaliyasi). */
  suspicious: boolean;
  /** Admin qo'lda belgilagan. */
  manual: boolean;
  /** Check-out bo'lmagani uchun avtomatik yopilgan. */
  autoClosed: boolean;
  /**
   * Fonda joylashuv kuzatuvi holati (LIVE-JOYLASHUV.md · Admin API). null — bugun check-in yo'q
   * yoki kuzatuv o'chiq. Eski backend maydonni umuman yubormasligi mumkin — `undefined` ham null kabi.
   */
  presence?: Presence | null;
}

/** Backend `PresenceState` (camelCase). */
export type PresenceState =
  'inside' | 'outside' | 'inaccurate' | 'signalLost' | 'stopped' | 'noConsent';

export interface Presence {
  state: PresenceState;
  /** UTC ISO; ping bo'lmagan bo'lsa null. */
  lastPingAt: string | null;
  /** Oxirgi ping'dan korxonagacha (m); null — noma'lum. */
  distanceM: number | null;
  /** Bugun tashqarida o'tgan daqiqalar. */
  outsideMinutes: number;
  /** Bugun kamida bitta soxta (isMocked) ping bo'lgan. */
  mocked: boolean;
}

/**
 * "Joyida" filtri (`?presence=`, backend `TodayPresenceFilter`) — holat filtridan alohida.
 * Hujjatda (LIVE-JOYLASHUV.md) yozilmagan, lekin backend qo'llaydi; UI joriy sahifani klientda ham filtrlaydi.
 */
export type PresenceFilter = 'all' | 'outside' | 'signalLost';

export const PRESENCE_FILTERS: { value: PresenceFilter; label: string }[] = [
  { value: 'all', label: 'Barchasi' },
  { value: 'outside', label: 'Tashqarida' },
  { value: 'signalLost', label: "Signal yo'q" },
];

export function isPresenceFilter(v: string | null): v is PresenceFilter {
  return v === 'all' || v === 'outside' || v === 'signalLost';
}

/** Kontraktdagi yagona sahifalash shakli. */
export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

/** GET /api/admin/today?status=&q=&page=&pageSize= */
export interface TodayResponse {
  /** DateOnly "2026-10-12" */
  date: string;
  stats: TodayStats;
  alerts: TodayAlert[];
  rows: Paged<AttendanceRow>;
}

/** Dizayndagi dayFilters (SPEC-NAV §4.2) — pill'lar; `excused`/`pending` faqat URL orqali. */
export const ATTENDANCE_FILTERS: { value: AttendanceFilter; label: string }[] = [
  { value: 'all', label: 'Hammasi' },
  { value: 'present', label: 'Keldi' },
  { value: 'late', label: 'Kech keldi' },
  { value: 'absent', label: 'Kelmadi' },
  { value: 'suspicious', label: 'Shubhali' },
];

const FILTER_VALUES: readonly AttendanceFilter[] = [
  'all',
  'present',
  'late',
  'absent',
  'excused',
  'pending',
  'suspicious',
];

export const ATTENDANCE_STATUS_LABEL: Record<
  AttendanceStatus,
  { label: string; kind: StatusKind }
> = {
  pending: { label: 'Kutilmoqda', kind: 'neu' },
  present: { label: 'Keldi', kind: 'ok' },
  late: { label: 'Kech keldi', kind: 'late' },
  absent: { label: 'Kelmadi', kind: 'bad' },
  excused: { label: 'Sababli', kind: 'info' },
  dayOff: { label: 'Dam olish', kind: 'neu' },
};

export const SUSPICIOUS_LABEL: { label: string; kind: StatusKind } = {
  label: 'Shubhali',
  kind: 'bad',
};

/** Qator badge'i: shubhali/radius tashqarisi → "Shubhali", aks holda holat. */
export function rowStatusLabel(row: AttendanceRow): { label: string; kind: StatusKind } {
  return row.suspicious || row.outOfRadius ? SUSPICIOUS_LABEL : ATTENDANCE_STATUS_LABEL[row.status];
}

export const DIARY_LABEL: Record<Exclude<DiaryState, null>, string> = {
  written: 'Yozildi',
  pending: 'Kutilmoqda',
};

export function isAttendanceFilter(v: string | null): v is AttendanceFilter {
  return v !== null && (FILTER_VALUES as readonly string[]).includes(v);
}

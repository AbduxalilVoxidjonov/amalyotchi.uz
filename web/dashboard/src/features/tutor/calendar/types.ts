/** Backend `CalendarDayStatus` (JSON camelCase). */
export type CalendarDayStatus =
  | 'future'
  | 'pending'
  | 'present'
  | 'late'
  | 'absent'
  | 'excused'
  | 'dayOff';

/** SPEC-TOKENS 1.5 — kalendar katak kodlari (CSS `data-code`). */
export type DayCode = 'k' | 'l' | 'a' | 's' | 'd' | 'n';

export interface CalendarRow {
  studentId: string;
  name: string;
  /** `days.length === days sarlavhasi uzunligi` */
  days: CalendarDayStatus[];
}

/** GET /api/tutor/calendar?month=YYYY-MM */
export interface CalendarResponse {
  /** "2026-10" */
  month: string;
  /** Oy kunlari 1..N. */
  days: number[];
  rows: CalendarRow[];
}

export interface DayCodeMeta {
  label: string;
  glyph: string;
}

export const DAY_CODE_META: Record<DayCode, DayCodeMeta> = {
  k: { label: 'Keldi', glyph: 'K' },
  l: { label: 'Kech keldi', glyph: 'k' },
  a: { label: 'Kelmadi', glyph: '×' },
  s: { label: 'Sababli', glyph: 'S' },
  d: { label: 'Dam olish', glyph: '·' },
  n: { label: 'Kelmagan kun', glyph: '' },
};

export const DAY_CODES: DayCode[] = ['k', 'l', 'a', 's', 'd', 'n'];

/** Backend holati → dizayn katak kodi (`future`/`pending` — bo'sh katak). */
export const DAY_STATUS_CODE: Record<CalendarDayStatus, DayCode> = {
  present: 'k',
  late: 'l',
  absent: 'a',
  excused: 's',
  dayOff: 'd',
  future: 'n',
  pending: 'n',
};

export const DAY_STATUS_LABEL: Record<CalendarDayStatus, string> = {
  present: 'Keldi',
  late: 'Kech keldi',
  absent: 'Kelmadi',
  excused: 'Sababli',
  dayOff: 'Dam olish',
  future: 'Kelmagan kun',
  pending: 'Kutilmoqda',
};

export function isDayCode(c: string): c is DayCode {
  return (DAY_CODES as string[]).includes(c);
}

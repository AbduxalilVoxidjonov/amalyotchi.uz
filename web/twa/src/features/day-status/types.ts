/**
 * Domain `CalendarDayStatus` (o'qishda hisoblanadi): future — hali kelmagan kun · pending — bugun,
 * oyna hali yopilmagan · present/late/absent/excused · dayOff — ish kuni emas / bayram.
 * SPEC-TOKENS 1.5 ranglari/glyph'lari `DAY_STATUS` da (k/l/a/s/d/n eski kodlari o'rniga enum).
 */
export type CalendarDayStatus =
  'future' | 'pending' | 'present' | 'late' | 'absent' | 'excused' | 'dayOff';

export interface CalendarDayDto {
  /** DateOnly */
  date: string;
  status: CalendarDayStatus;
}

/** GET /api/student/calendar?month=YYYY-MM (`CalendarMonthDto`). */
export interface CalendarMonthDto {
  /** "2026-10" */
  month: string;
  studentName: string;
  groupName: string;
  /** Oyning har kuni uchun (1..N), tartib bilan. */
  days: CalendarDayDto[];
}

export const DAY_STATUS: Record<CalendarDayStatus, { label: string; glyph: string }> = {
  present: { label: 'Keldi', glyph: 'K' },
  late: { label: 'Kech keldi', glyph: 'k' },
  absent: { label: 'Kelmadi', glyph: '×' },
  excused: { label: 'Sababli', glyph: 'S' },
  dayOff: { label: 'Dam olish', glyph: '·' },
  pending: { label: 'Kutilmoqda', glyph: '…' },
  future: { label: 'Kelgusi kun', glyph: '' },
};

/** Legend tartibi. */
export const DAY_STATUS_ORDER: CalendarDayStatus[] = [
  'present',
  'late',
  'absent',
  'excused',
  'dayOff',
  'pending',
  'future',
];

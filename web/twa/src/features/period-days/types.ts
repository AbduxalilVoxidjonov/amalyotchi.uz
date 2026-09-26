import type { CalendarDayStatus } from '@/features/day-status/types';
import type { DiaryStatus } from '@/features/diary/types';
import type { StudentPeriodOption, StudentPeriodStatus } from '@/features/period/types';
import { WEEKDAYS_UZ, parseDateOnly } from '@/shared/lib/format';

/**
 * GET /api/student/period-days?periodId=<guid?> — bosh ekran: amaliyot davri va uning har bir kuni.
 * Holatlar kalendar bilan bir xil enum (`CalendarDayStatus`) — yorliq/rang `DAY_STATUS` dan.
 */
export type PeriodDayStatus = CalendarDayStatus;

export interface PeriodDayDiary {
  id: string;
  status: DiaryStatus;
  /** 1–5 (tasdiqlanganda). */
  score: number | null;
}

export interface PeriodDay {
  /** DateOnly "2026-09-24" */
  date: string;
  /** 1 = Dushanba … 7 = Yakshanba */
  weekday: number;
  isWorkDay: boolean;
  /** Bayram nomi (ish kuni emas). */
  holiday: string | null;
  status: PeriodDayStatus;
  /** "HH:mm" (Toshkent) */
  checkInAt: string | null;
  checkOutAt: string | null;
  autoClosed: boolean;
  suspicious: boolean;
  manual: boolean;
  diary: PeriodDayDiary | null;
}

export interface PeriodSummary {
  id: string;
  name: string;
  status: StudentPeriodStatus;
  startDate: string;
  endDate: string;
  requiredDays: number;
  elapsedWorkDays: number;
}

export interface StudentPeriodDays {
  /** Server sanasi (DateOnly, Toshkent). */
  today: string;
  /** Tanlangan (yoki sukut) davr; talabaga davr biriktirilmagan → null. */
  period: PeriodSummary | null;
  /** Talabaning barcha davrlari (tanlagich uchun). */
  periods: StudentPeriodOption[];
  /** Davrning har bir kuni, xronologik tartibda. */
  days: PeriodDay[];
}

/** 1 (Du) … 7 (Ya) → "Payshanba". */
export function weekdayName(weekday: number): string {
  return WEEKDAYS_UZ[weekday % 7] ?? '';
}

/** "2026-09-24" → "24.09". */
export function formatDayMonth(date: string): string {
  const p = parseDateOnly(date);
  if (!p) return date;
  return `${String(p.d).padStart(2, '0')}.${String(p.m).padStart(2, '0')}`;
}

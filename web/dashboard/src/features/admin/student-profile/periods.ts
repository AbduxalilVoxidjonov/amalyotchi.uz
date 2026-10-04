import { useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { StatusKind } from '@/shared/ui';
import { fmtDateOnly, fmtDayMonth, shiftMonth } from '../format';
import type { StudentPeriodOption } from './types';

/**
 * Talaba profilidagi davr tanlagichi yordamchilari (API v3.5, §4.6). Admin va tyutor profillari
 * bir xil komponentlarni ishlatadi, shuning uchun hammasi shu yerda.
 */

/** Ko'rinadigan holat: backend `active` ni `endDate < bugun` bo'lsa "Tugagan" ga ajratamiz. */
export type PeriodPhase = 'planned' | 'active' | 'ended' | 'closed';

export const PERIOD_PHASE_LABEL: Record<PeriodPhase, { label: string; kind: StatusKind }> = {
  planned: { label: 'Rejada', kind: 'info' },
  active: { label: 'Faol', kind: 'ok' },
  ended: { label: 'Tugagan', kind: 'late' },
  closed: { label: 'Yopilgan', kind: 'neu' },
};

/** `today` — Toshkent DateOnly ("2026-09-23"). */
export function periodPhase(
  p: Pick<StudentPeriodOption, 'status' | 'endDate'>,
  today: string,
): PeriodPhase {
  if (p.status === 'active' && p.endDate < today) return 'ended';
  return p.status;
}

/** "2026-08-31" + "2026-10-14" → "31.08 — 14.10.2026"; yillar farq qilsa boshida ham yil. */
export function fmtPeriodDates(p: Pick<StudentPeriodOption, 'startDate' | 'endDate'>): string {
  const start =
    p.startDate.slice(0, 4) === p.endDate.slice(0, 4)
      ? fmtDayMonth(p.startDate)
      : fmtDateOnly(p.startDate);
  return `${start} — ${fmtDateOnly(p.endDate)}`;
}

/** Rejadagi davr bo'limlaridagi bo'sh holat matni. */
export function plannedPeriodText(p: Pick<StudentPeriodOption, 'startDate'>): string {
  return `Bu davr ${fmtDateOnly(p.startDate)} dan boshlanadi`;
}

export interface MonthCursor {
  year: number;
  month: number;
}

const toCursor = (dateOnly: string): MonthCursor => ({
  year: Number(dateOnly.slice(0, 4)),
  month: Number(dateOnly.slice(5, 7)),
});

const monthIndex = (c: MonthCursor) => c.year * 12 + c.month;

export function compareMonth(a: MonthCursor, b: MonthCursor): number {
  return monthIndex(a) - monthIndex(b);
}

/** Davr oylari chegarasi (birinchi va oxirgi oy). */
export function periodMonthRange(p: Pick<StudentPeriodOption, 'startDate' | 'endDate'>): {
  first: MonthCursor;
  last: MonthCursor;
} {
  return { first: toCursor(p.startDate), last: toCursor(p.endDate) };
}

/**
 * Davr tanlanganda kalendar ochiladigan oy — "oxirgi faol oy": `min(bugun, endDate)` oyi,
 * davr boshidan oldin bo'lmaydi (rejadagi davr → birinchi oy).
 */
export function defaultPeriodMonth(
  p: Pick<StudentPeriodOption, 'startDate' | 'endDate'>,
  today: string,
): MonthCursor {
  const anchor = p.endDate < today ? p.endDate : today;
  const { first } = periodMonthRange(p);
  const cursor = toCursor(anchor);
  return compareMonth(cursor, first) < 0 ? first : cursor;
}

/** Oy navigatsiyasi: davr chegarasidan chiqmaydi (chegarada bo'lsa o'zgarmaydi). */
export function stepMonthWithin(
  cursor: MonthCursor,
  delta: number,
  p: Pick<StudentPeriodOption, 'startDate' | 'endDate'> | null,
): MonthCursor {
  const next = shiftMonth(cursor.year, cursor.month, delta);
  if (!p) return next;
  const { first, last } = periodMonthRange(p);
  if (compareMonth(next, first) < 0) return first;
  if (compareMonth(next, last) > 0) return last;
  return next;
}

/** URL'dagi `?period=<id>` — tanlangan davr. Yo'q bo'lsa `null` (backend sukut davrini tanlaydi). */
export const PERIOD_SEARCH_PARAM = 'period';

export function usePeriodParam(): [string | null, (periodId: string | null) => void] {
  const [params, setParams] = useSearchParams();
  const value = params.get(PERIOD_SEARCH_PARAM) || null;
  const setValue = useCallback(
    (periodId: string | null) => {
      setParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          if (periodId) next.set(PERIOD_SEARCH_PARAM, periodId);
          else next.delete(PERIOD_SEARCH_PARAM);
          return next;
        },
        { replace: true },
      );
    },
    [setParams],
  );
  return [value, setValue];
}

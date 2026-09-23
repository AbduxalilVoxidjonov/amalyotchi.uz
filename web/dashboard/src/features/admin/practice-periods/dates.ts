import { APP_TIME_ZONE } from '@/shared/lib/date';
import { DASH, formatDayMonth } from '../shared/format';

/**
 * Amaliyot davri sanalari (`DateOnly` "YYYY-MM-DD"). Hammasi matn/UTC bo'yicha hisoblanadi —
 * brauzer vaqt zonasi natijaga ta'sir qilmaydi. ISO sanalarni satr sifatida solishtirish to'g'ri.
 */
const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

export function isIsoDate(v: string): boolean {
  return ISO_DATE.test(v) && !Number.isNaN(Date.parse(`${v}T00:00:00Z`));
}

/** "2026-09-01" → "01.09.2026". */
export function formatDate(d: string): string {
  return formatDayMonth(d, true);
}

/** "01.09.2026 — 30.10.2026". */
export function formatRange(start: string, end: string): string {
  return `${formatDate(start)} — ${formatDate(end)}`;
}

/** "01.09 — 30.10" (band guruh yozuvi uchun, yilsiz). */
export function formatShortRange(start: string, end: string): string {
  return `${formatDayMonth(start)} — ${formatDayMonth(end)}`;
}

/** Ikkala chegara kiritilgan kalendar kunlar soni; noto'g'ri/teskari oraliq — `null`. */
export function calendarDays(start: string, end: string): number | null {
  if (!isIsoDate(start) || !isIsoDate(end) || end < start) return null;
  const ms = Date.parse(`${end}T00:00:00Z`) - Date.parse(`${start}T00:00:00Z`);
  return Math.round(ms / 86_400_000) + 1;
}

export function formatDays(n: number | null): string {
  return n === null ? DASH : `${n} kun`;
}

/** Yopiq oraliqlar kesishadimi (chegaralar kiradi). */
export function rangesOverlap(aStart: string, aEnd: string, bStart: string, bEnd: string): boolean {
  return aStart <= bEnd && bStart <= aEnd;
}

const tashkentDay = new Intl.DateTimeFormat('en-CA', {
  timeZone: APP_TIME_ZONE,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

/** Bugungi sana Toshkent vaqti bo'yicha, "YYYY-MM-DD". */
export function todayIso(now: Date = new Date()): string {
  return tashkentDay.format(now);
}

/** ISO hafta kunlari: 1 — Dushanba … 7 — Yakshanba (backend `WorkDays`). */
export const WEEKDAYS = [
  { day: 1, short: 'Du', name: 'Dushanba' },
  { day: 2, short: 'Se', name: 'Seshanba' },
  { day: 3, short: 'Ch', name: 'Chorshanba' },
  { day: 4, short: 'Pa', name: 'Payshanba' },
  { day: 5, short: 'Ju', name: 'Juma' },
  { day: 6, short: 'Sh', name: 'Shanba' },
  { day: 7, short: 'Ya', name: 'Yakshanba' },
] as const;

export function parseWorkDays(csv: string): Set<number> {
  return new Set(
    csv
      .split(',')
      .map((p) => Number(p.trim()))
      .filter((n) => Number.isInteger(n) && n >= 1 && n <= 7),
  );
}

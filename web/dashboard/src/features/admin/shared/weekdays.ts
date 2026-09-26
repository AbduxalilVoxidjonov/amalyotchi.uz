/**
 * ISO hafta kunlari: 1 — Dushanba … 7 — Yakshanba (backend `WorkDays`, CSV "1,2,3,4,5,6").
 * Sozlamalar (global `workDays`) va amaliyot davri (davr `workDays`) uchun umumiy yordamchilar.
 */
export const WEEKDAYS = [
  { day: 1, short: 'Du', name: 'Dushanba' },
  { day: 2, short: 'Se', name: 'Seshanba' },
  { day: 3, short: 'Ch', name: 'Chorshanba' },
  { day: 4, short: 'Pa', name: 'Payshanba' },
  { day: 5, short: 'Ju', name: 'Juma' },
  { day: 6, short: 'Sh', name: 'Shanba' },
  { day: 7, short: 'Ya', name: 'Yakshanba' },
] as const;

/** CSV → kunlar to'plami (noto'g'ri qismlar tashlab yuboriladi). */
export function parseWeekdays(csv: string): Set<number> {
  return new Set(
    csv
      .split(',')
      .map((p) => Number(p.trim()))
      .filter((n) => Number.isInteger(n) && n >= 1 && n <= 7),
  );
}

/** Kunlar → tartiblangan CSV ("1,2,3,4,5"). */
export function formatWeekdays(days: Iterable<number>): string {
  return [...new Set(days)].sort((a, b) => a - b).join(',');
}

/** CSV'ni tozalab, tartiblangan ko'rinishga keltiradi (" 3,1,1" → "1,3"). */
export function normalizeWeekdays(csv: string): string {
  return formatWeekdays(parseWeekdays(csv));
}

/** Kunni qo'shadi/olib tashlaydi → tartiblangan CSV ("1,2,3,4,5,6"). */
export function toggleWeekday(csv: string, day: number): string {
  const days = parseWeekdays(csv);
  if (days.has(day)) days.delete(day);
  else days.add(day);
  return formatWeekdays(days);
}

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const DAY_MS = 86_400_000;

/**
 * `start..end` (ikkala chegara kiradi) oralig'idagi ish kunlari soni — bayramlarsiz, taxminiy.
 * Noto'g'ri/teskari oraliq — `null`. Hisob UTC bo'yicha (brauzer zonasi ta'sir qilmaydi).
 */
export function countWorkDays(start: string, end: string, csv: string): number | null {
  if (!ISO_DATE.test(start) || !ISO_DATE.test(end) || end < start) return null;
  const from = Date.parse(`${start}T00:00:00Z`);
  const to = Date.parse(`${end}T00:00:00Z`);
  if (Number.isNaN(from) || Number.isNaN(to)) return null;
  const days = parseWeekdays(csv);
  if (days.size === 0) return 0;
  let n = 0;
  for (let t = from; t <= to; t += DAY_MS) {
    const iso = ((new Date(t).getUTCDay() + 6) % 7) + 1;
    if (days.has(iso)) n++;
  }
  return n;
}

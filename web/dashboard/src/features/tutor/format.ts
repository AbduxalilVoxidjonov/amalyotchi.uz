import { APP_TIME_ZONE } from '@/shared/lib/date';

/** 4.2 → "4,2" (o'zbekcha kasr ajratgichi — dizayndagi kabi). */
export function fmtDecimal(value: number, digits = 1): string {
  return value.toFixed(digits).replace('.', ',');
}

/** 45 → "45 m", 3400 → "3,4 km". */
export function fmtDistance(meters: number): string {
  if (meters >= 1000) return `${fmtDecimal(meters / 1000)} km`;
  return `${Math.round(meters)} m`;
}

const partsFmt = new Intl.DateTimeFormat('en-GB', {
  timeZone: APP_TIME_ZONE,
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

function parts(iso: string): Record<string, string> | null {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return null;
  const p: Record<string, string> = {};
  for (const part of partsFmt.formatToParts(d)) p[part.type] = part.value;
  return p;
}

/** ISO → "17:42" (Toshkent vaqti). */
export function fmtTime(iso: string): string {
  const p = parts(iso);
  return p ? `${p['hour']}:${p['minute']}` : '';
}

/** ISO → "11.10.2026" (Toshkent vaqti; `shared/lib/date.formatDate` uz-UZ da "/" beradi). */
export function fmtDate(iso: string): string {
  const p = parts(iso);
  return p ? `${p['day']}.${p['month']}.${p['year']}` : '';
}

/** Bugungi kun Toshkent bo'yicha — "2026-09-14" (DateOnly). */
export function todayInTashkent(now: number = Date.now()): string {
  const p = parts(new Date(now).toISOString());
  return p ? `${p['year']}-${p['month']}-${p['day']}` : new Date(now).toISOString().slice(0, 10);
}

/** "2026-10-14" (DateOnly) → "14.10". */
export function fmtDayMonth(dateOnly: string): string {
  const [, m, d] = dateOnly.split('-');
  return m && d ? `${d}.${m}` : dateOnly;
}

/** "2026-10-14" → "14.10.2026". */
export function fmtDateOnly(dateOnly: string): string {
  const [y, m, d] = dateOnly.split('-');
  return y && m && d ? `${d}.${m}.${y}` : dateOnly;
}

export const MONTHS_UZ = [
  'Yanvar',
  'Fevral',
  'Mart',
  'Aprel',
  'May',
  'Iyun',
  'Iyul',
  'Avgust',
  'Sentabr',
  'Oktabr',
  'Noyabr',
  'Dekabr',
] as const;

/** "2026-10" → { year: 2026, month: 10 } (1-asoslangan). */
export function parseMonth(
  value: string | null | undefined,
): { year: number; month: number } | null {
  if (!value) return null;
  const m = /^(\d{4})-(\d{2})$/.exec(value);
  if (!m) return null;
  const year = Number(m[1]);
  const month = Number(m[2]);
  if (month < 1 || month > 12) return null;
  return { year, month };
}

export function toMonthParam(year: number, month: number): string {
  return `${year}-${String(month).padStart(2, '0')}`;
}

export function shiftMonth(
  year: number,
  month: number,
  delta: number,
): { year: number; month: number } {
  const idx = year * 12 + (month - 1) + delta;
  return { year: Math.floor(idx / 12), month: (idx % 12) + 1 };
}

export function monthLabel(year: number, month: number): string {
  return `${MONTHS_UZ[month - 1]} ${year}`;
}

/** "2026-08-31" + "2026-10-14" → "31.08.2026 — 14.10.2026" (biri yo'q bo'lsa "—"). */
export function fmtDateRange(from: string | null, to: string | null): string {
  if (!from && !to) return '—';
  return `${from ? fmtDateOnly(from) : '…'} — ${to ? fmtDateOnly(to) : '…'}`;
}

/** "2026-09-16" · "2026-09-17"–"2026-09-18" → "16.09" · "17.09–18.09". */
export function fmtDayRange(from: string, to: string | null): string {
  return to && to !== from ? `${fmtDayMonth(from)}–${fmtDayMonth(to)}` : fmtDayMonth(from);
}

/** ISO → "2 soat oldin" / "3 kun oldin" / "hozirgina" (now — test uchun almashtiriladi). */
export function fmtRelative(iso: string, now: number = Date.now()): string {
  const t = new Date(iso).getTime();
  if (Number.isNaN(t)) return '';
  const diffMin = Math.max(0, Math.round((now - t) / 60_000));
  if (diffMin < 1) return 'hozirgina';
  if (diffMin < 60) return `${diffMin} daqiqa oldin`;
  const hours = Math.floor(diffMin / 60);
  if (hours < 24) return `${hours} soat oldin`;
  const days = Math.floor(hours / 24);
  if (days < 30) return `${days} kun oldin`;
  return `${fmtDate(iso)} da`;
}

/** ISO → shu kundan beri necha kun (0 = bugun). */
export function daysSince(iso: string, now: number = Date.now()): number {
  const t = new Date(iso).getTime();
  if (Number.isNaN(t)) return 0;
  return Math.max(0, Math.floor((now - t) / 86_400_000));
}

/** 1 843 200 → "1,8 MB" · 512 000 → "500 KB" · 900 → "900 B". */
export function fmtBytes(bytes: number): string {
  if (bytes >= 1024 * 1024) return `${fmtDecimal(bytes / (1024 * 1024))} MB`;
  if (bytes >= 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${bytes} B`;
}

/** "304512889" → "304 512 889" (STIR). */
export function fmtTin(tin: string): string {
  const digits = tin.replace(/\D/g, '');
  return digits.length === 9 ? digits.replace(/(\d{3})(\d{3})(\d{3})/, '$1 $2 $3') : tin;
}

/** "+998901234567" → "+998 90 123 45 67". */
export function fmtPhone(phone: string): string {
  const m = /^\+?998(\d{2})(\d{3})(\d{2})(\d{2})$/.exec(phone.replace(/[\s-]/g, ''));
  return m ? `+998 ${m[1]} ${m[2]} ${m[3]} ${m[4]}` : phone;
}

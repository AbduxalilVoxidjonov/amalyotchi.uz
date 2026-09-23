/**
 * Formatlash yordamchilari. Backend `DateOnly` → "2026-10-12", `DateTimeOffset` → ISO 8601.
 * UI'da Toshkent vaqti (Asia/Tashkent) va o'zbek yozuvi (12.10.2026, 4,2) ko'rsatiladi.
 */
export const APP_TIME_ZONE = 'Asia/Tashkent';

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

export const WEEKDAYS_UZ = [
  'Yakshanba',
  'Dushanba',
  'Seshanba',
  'Chorshanba',
  'Payshanba',
  'Juma',
  'Shanba',
] as const;

/** Kalendar sarlavhasi uchun qisqa kun nomlari (dushanbadan boshlab). */
export const WEEKDAYS_SHORT_UZ = ['Du', 'Se', 'Ch', 'Pa', 'Ju', 'Sh', 'Ya'] as const;

const pad2 = (n: number) => String(n).padStart(2, '0');

/** "2026-10-12" | ISO datetime → { y, m, d } (DateOnly uchun UTC-siz, faqat sana qismi). */
export function parseDateOnly(value: string): { y: number; m: number; d: number } | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (!m) return null;
  return { y: Number(m[1]), m: Number(m[2]), d: Number(m[3]) };
}

/** "2026-10-12" → "12.10.2026". */
export function formatDate(value: string | null | undefined): string {
  if (!value) return '';
  const p = parseDateOnly(value);
  return p ? `${pad2(p.d)}.${pad2(p.m)}.${p.y}` : '';
}

/** ISO datetime → "09:02" (Toshkent vaqti). */
export function formatTime(value: string | null | undefined): string {
  if (!value) return '';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  return new Intl.DateTimeFormat('en-GB', {
    timeZone: APP_TIME_ZONE,
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(d);
}

/** "2026-10-12" → "Oktabr 2026". */
export function formatMonthLabel(month: string): string {
  const p = parseDateOnly(`${month}-01`);
  if (!p) return month;
  return `${MONTHS_UZ[p.m - 1] ?? ''} ${p.y}`;
}

/** "2026-10" ± n oy → "2026-11". */
export function shiftMonth(month: string, delta: number): string {
  const p = parseDateOnly(`${month}-01`);
  if (!p) return month;
  const total = p.y * 12 + (p.m - 1) + delta;
  const y = Math.floor(total / 12);
  const m = (total % 12) + 1;
  return `${y}-${pad2(m)}`;
}

/** Date → "2026-10" (mahalliy). */
export function toMonthKey(d: Date): string {
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}`;
}

/** Date → "2026-10-12" (mahalliy). */
export function toDateOnly(d: Date): string {
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
}

/** 4.2 → "4,2" (o'zbek yozuvi, vergul). */
export function formatDecimal(n: number, digits = 1): string {
  return n.toFixed(digits).replace('.', ',');
}

/** 94.4 → "94%". */
export function formatPercent(n: number): string {
  return `${Math.round(n)}%`;
}

/** 45 → "45 m"; null → "—". */
export function formatMeters(n: number | null | undefined): string {
  return n === null || n === undefined ? '—' : `${Math.round(n)} m`;
}

/** "14.10.2026" (foydalanuvchi kiritgan) → "2026-10-14" yoki null. */
export function parseUserDate(input: string): string | null {
  const m = /^(\d{2})\.(\d{2})\.(\d{4})$/.exec(input.trim());
  if (!m) return null;
  const d = Number(m[1]);
  const mo = Number(m[2]);
  const y = Number(m[3]);
  const date = new Date(Date.UTC(y, mo - 1, d));
  if (date.getUTCFullYear() !== y || date.getUTCMonth() !== mo - 1 || date.getUTCDate() !== d) {
    return null;
  }
  return `${y}-${pad2(mo)}-${pad2(d)}`;
}

/** "+998901112233" → "+998 90 111 22 33"; boshqa formatlar o'zgarishsiz. */
export function formatPhone(value: string | null | undefined): string {
  if (!value) return '';
  const m = /^\+998(\d{2})(\d{3})(\d{2})(\d{2})$/.exec(value.replace(/[\s-]/g, ''));
  return m ? `+998 ${m[1]} ${m[2]} ${m[3]} ${m[4]}` : value;
}

/** "2026-10-01", "2026-11-15" → "01.10–15.11.2026" (yil bir xil bo'lsa qisqa). */
export function formatPeriod(from: string, to: string): string {
  const f = formatDate(from);
  const t = formatDate(to);
  if (!f || !t) return f || t;
  return f.slice(-4) === t.slice(-4) ? `${f.slice(0, 5)}–${t}` : `${f}–${t}`;
}

/** Ikki DateOnly orasidagi kunlar: "2026-12-20" → "2027-02-01" = 43 (manfiy ham bo'lishi mumkin). */
export function daysBetween(from: string, to: string): number {
  const a = parseDateOnly(from);
  const b = parseDateOnly(to);
  if (!a || !b) return 0;
  return Math.round((Date.UTC(b.y, b.m - 1, b.d) - Date.UTC(a.y, a.m - 1, a.d)) / 86_400_000);
}

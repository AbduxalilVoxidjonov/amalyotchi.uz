/**
 * Sana yordamchilari. Backend `DateTimeOffset` / `DateOnly` ni ISO 8601 ko'rinishida yuboradi.
 * UI'da Toshkent vaqti (Asia/Tashkent, UTC+5) ko'rsatiladi.
 */
export const APP_TIME_ZONE = 'Asia/Tashkent';
export const APP_LOCALE = 'uz-UZ';

export function parseDate(value: string | Date | null | undefined): Date | null {
  if (!value) return null;
  const d = value instanceof Date ? value : new Date(value);
  return Number.isNaN(d.getTime()) ? null : d;
}

const dateFmt = new Intl.DateTimeFormat(APP_LOCALE, {
  timeZone: APP_TIME_ZONE,
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
});

const dateTimeFmt = new Intl.DateTimeFormat(APP_LOCALE, {
  timeZone: APP_TIME_ZONE,
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

/** 14.09.2026 */
export function formatDate(value: string | Date | null | undefined): string {
  const d = parseDate(value);
  return d ? dateFmt.format(d) : '';
}

/** 14.09.2026, 19:30 */
export function formatDateTime(value: string | Date | null | undefined): string {
  const d = parseDate(value);
  return d ? dateTimeFmt.format(d) : '';
}

/** `DateOnly` uchun: 2026-09-14 (local emas, UTC-siz — faqat sana qismi). */
export function toDateOnly(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

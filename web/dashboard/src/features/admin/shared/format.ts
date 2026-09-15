import { APP_TIME_ZONE } from '@/shared/lib/date';

/**
 * Admin ekranlari uchun formatlash. Backend v2 xom qiymat yuboradi (raqam, ISO sana, E.164 telefon,
 * 9 raqamli STIR) — matn shakli faqat shu yerda yasaladi.
 */

/** Ming ajratgich — thin space (U+2009): mono 30px qiymatda oddiy bo'shliq juda keng ko'rinadi. */
export const THIN_SPACE = '\u2009';

/** Bo'sh qiymat belgisi. */
export const DASH = '—';

/** 1284 → "1 284" (dizayndagi stat qiymatlari; ajratgich — thin space). */
export function formatCount(n: number): string {
  return String(n).replace(/\B(?=(\d{3})+(?!\d))/g, THIN_SPACE);
}

/** 91 → "91%". */
export function formatPct(pct: number): string {
  return `${pct}%`;
}

const shortFmt = new Intl.DateTimeFormat('ru-RU', {
  timeZone: APP_TIME_ZONE,
  day: '2-digit',
  month: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
});

function parts(fmt: Intl.DateTimeFormat, d: Date): Record<string, string> {
  return Object.fromEntries(fmt.formatToParts(d).map((x) => [x.type, x.value]));
}

const fullFmt = new Intl.DateTimeFormat('ru-RU', {
  timeZone: APP_TIME_ZONE,
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
});

/** ISO → "20.08.2026 14:00" (yil bilan — yaratilgan sana kabi bir martalik vaqtlar, Toshkent vaqti). */
export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return DASH;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return DASH;
  const p = parts(fullFmt, d);
  return `${p['day']}.${p['month']}.${p['year']} ${p['hour']}:${p['minute']}`;
}

/** ISO → "12.10 09:31" (audit jurnali vaqti, Toshkent vaqti). */
export function formatShortDateTime(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return DASH;
  const p = parts(shortFmt, d);
  return `${p['day']}.${p['month']} ${p['hour']}:${p['minute']}`;
}

/**
 * `DateOnly` ("2000-03-08") → "08.03" (takrorlanuvchi bayram — yil ko'rsatilmaydi);
 * `withYear` bo'lsa "08.03.2027". Vaqt zonasi ta'sir qilmasin deb matn bo'yicha ajratiladi.
 */
export function formatDayMonth(dateOnly: string, withYear = false): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(dateOnly);
  if (!m) return DASH;
  const [, y, mo, d] = m;
  return withYear ? `${d}.${mo}.${y}` : `${d}.${mo}`;
}

/** E.164 "+998901234567" → "+998 90 123-45-67". Boshqa shakl — o'zgarishsiz. */
export function formatPhone(phone: string | null | undefined): string {
  if (!phone) return DASH;
  const m = /^\+998(\d{2})(\d{3})(\d{2})(\d{2})$/.exec(phone);
  return m ? `+998 ${m[1]} ${m[2]}-${m[3]}-${m[4]}` : phone;
}

/** STIR "305881204" → "305 881 204". */
export function formatTin(tin: string): string {
  return /^\d{9}$/.test(tin) ? tin.replace(/(\d{3})(\d{3})(\d{3})/, '$1 $2 $3') : tin;
}

/** O'nlik: 1.44 → "1.4", 2.0 → "2". */
function decimal1(n: number): string {
  return String(Math.round(n * 10) / 10);
}

/**
 * Qaror tezligi (soatlarda) → "1.4 soat" / "2 kun". `null` — hali qaror yo'q ("—").
 * 1 soatdan kam — "35 daqiqa".
 */
export function formatHours(hours: number | null | undefined): string {
  if (hours == null || !Number.isFinite(hours)) return DASH;
  if (hours < 1) return `${Math.max(1, Math.round(hours * 60))} daqiqa`;
  if (hours < 24) return `${decimal1(hours)} soat`;
  return `${decimal1(hours / 24)} kun`;
}

const MIN = 60_000;
const HOUR = 60 * MIN;
const DAY = 24 * HOUR;

/** ISO → "hozirgina" / "5 daqiqa oldin" / "2 soat oldin" / "3 kun oldin" / (30 kundan eski) "12.10 09:31". */
export function formatRelative(iso: string | null | undefined, now: number = Date.now()): string {
  if (!iso) return DASH;
  const t = new Date(iso).getTime();
  if (Number.isNaN(t)) return DASH;
  const diff = Math.max(0, now - t);
  if (diff < MIN) return 'hozirgina';
  if (diff < HOUR) return `${Math.floor(diff / MIN)} daqiqa oldin`;
  if (diff < DAY) return `${Math.floor(diff / HOUR)} soat oldin`;
  if (diff < 30 * DAY) return `${Math.floor(diff / DAY)} kun oldin`;
  return formatShortDateTime(iso);
}

/** Tyutor biriktirilgan doirasi: "AT · 412-22, 413-22"; 3 tadan ko'p guruh — "AT · 5 guruh". */
export function formatScope(
  facultyCode: string | null | undefined,
  groups: readonly string[],
): string {
  const groupText =
    groups.length === 0
      ? "guruh yo'q"
      : groups.length > 3
        ? `${groups.length} guruh`
        : groups.join(', ');
  return facultyCode ? `${facultyCode} · ${groupText}` : groupText;
}

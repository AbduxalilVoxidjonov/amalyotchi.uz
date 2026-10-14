/**
 * "Joylashuvni kuzatish" sozlamalari (LIVE-JOYLASHUV.md): klient tomonidagi oraliq tekshiruvi —
 * backend ham shu oraliqlarni tekshiradi (400 `errors{key}`), bu yerda faqat erta xabar va
 * Saqlash'ni to'sish uchun.
 */
export const TRACKING_ENABLED_KEY = 'trackingEnabled';
export const TRACKING_INTERVAL_KEY = 'trackingIntervalSec';
export const TRACKING_DISTANCE_KEY = 'trackingDistanceFilterM';

/** `trackingEnabled` o'chiq bo'lsa bu maydonlar tahrirlanmaydi (kuzatuv yo'q — oraliq/masofa ma'nosiz). */
const TRACKING_DEPENDENT_KEYS: ReadonlySet<string> = new Set([
  TRACKING_INTERVAL_KEY,
  TRACKING_DISTANCE_KEY,
]);

/** Kalit kuzatuv o'chiqligi sababli disabled bo'lishi kerakmi (`values` — joriy draft/server qiymatlari). */
export function isTrackingDependentDisabled(
  key: string,
  values: Readonly<Record<string, string>>,
): boolean {
  return TRACKING_DEPENDENT_KEYS.has(key) && values[TRACKING_ENABLED_KEY] === 'false';
}

export const TRACKING_LIMITS = {
  [TRACKING_INTERVAL_KEY]: { min: 60, max: 900 },
  [TRACKING_DISTANCE_KEY]: { min: 10, max: 500 },
} as const;

export const TRACKING_ERRORS = {
  [TRACKING_INTERVAL_KEY]: "Ping oralig'i 1–15 daqiqa (60–900 soniya) bo'lishi kerak.",
  [TRACKING_DISTANCE_KEY]: "Masofa filtri 10–500 m oralig'ida butun son bo'lishi kerak.",
} as const;

function inRange(raw: string | undefined, min: number, max: number): boolean {
  const v = (raw ?? '').trim();
  if (!/^\d+$/.test(v)) return false;
  const n = Number(v);
  return n >= min && n <= max;
}

/** Faqat sozlamalar ro'yxatida bor kalitlar tekshiriladi (eski backend — kalit yo'q → xato yo'q). */
export function validateTracking(
  values: Record<string, string>,
  presentKeys: ReadonlySet<string>,
): Record<string, string[]> {
  const errors: Record<string, string[]> = {};
  for (const key of [TRACKING_INTERVAL_KEY, TRACKING_DISTANCE_KEY] as const) {
    if (!presentKeys.has(key)) continue;
    const { min, max } = TRACKING_LIMITS[key];
    if (!inRange(values[key], min, max)) errors[key] = [TRACKING_ERRORS[key]];
  }
  return errors;
}

/** 120 → "2", 90 → "1.5" (input uchun; noto'g'ri qiymat — o'zicha). */
export function secondsToMinutesText(raw: string): string {
  const v = raw.trim();
  if (!/^\d+$/.test(v)) return v;
  const min = Number(v) / 60;
  return Number.isInteger(min) ? String(min) : String(Math.round(min * 10) / 10);
}

/** "2" → "120", "1,5" → "90"; son bo'lmasa — matnning o'zi (validatsiya ushlaydi). */
export function minutesTextToSeconds(text: string): string {
  const v = text.trim().replace(',', '.');
  if (v === '' || !/^\d+(\.\d*)?$/.test(v)) return text.trim();
  return String(Math.round(Number(v) * 60));
}

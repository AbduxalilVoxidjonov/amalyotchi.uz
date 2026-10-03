import { APP_TIME_ZONE } from '@/shared/lib/format';
import type { StudentProfileDto, StudentProfilePracticeDto, StudentWorkHoursDto } from './types';

/**
 * Profildagi barcha amaliyot davrlari. `practices` bo'lmasa (eski server) — `practice` ni yagona element
 * sifatida qaytaradi. Tartib (kontrakt bo'yicha, himoya uchun klientda ham): davom etayotgan (`active`) davr
 * birinchi, keyin `startDate` kamayish tartibida.
 */
export function profilePractices(profile: StudentProfileDto): StudentProfilePracticeDto[] {
  const list = profile.practices ?? (profile.practice ? [profile.practice] : []);
  return [...list].sort((a, b) => {
    const aActive = a.period.status === 'active' ? 0 : 1;
    const bActive = b.period.status === 'active' ? 0 : 1;
    if (aActive !== bActive) return aActive - bActive;
    return b.period.startDate.localeCompare(a.period.startDate);
  });
}

/** Toshkent bo'yicha bugungi sana ("2026-10-03") — `effectiveFrom` bilan solishtirish uchun. */
export function tashkentToday(now: Date = new Date()): string {
  // en-CA — "yyyy-MM-dd" formati.
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: APP_TIME_ZONE,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(now);
}

/** "2026-10-03" + 1 kun → "2026-10-04" (UTC hisobida, vaqt mintaqasidan mustaqil). */
export function addDays(dateOnly: string, days: number): string {
  const d = new Date(`${dateOnly}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

/** "09:30" | "09:30:00" → 570; noto'g'ri → null. */
export function timeToMinutes(value: string): number | null {
  const m = /^(\d{2}):(\d{2})/.exec(value);
  if (!m) return null;
  const h = Number(m[1]);
  const min = Number(m[2]);
  return h < 24 && min < 60 ? h * 60 + min : null;
}

/** Ish vaqtining minimal davomiyligi (server qoidasi bilan bir xil). */
export const MIN_WORK_MINUTES = 60;

export type WorkHoursErrors = Partial<Record<'start' | 'end', string>>;

/** Server qoidalarining klient nusxasi: ikkalasi kiritilgan, ketish > kelish, kamida 1 soat. */
export function validateWorkHours(start: string, end: string): WorkHoursErrors {
  const e: WorkHoursErrors = {};
  const s = start ? timeToMinutes(start) : null;
  const en = end ? timeToMinutes(end) : null;
  if (!start) e.start = 'Kelish vaqtini kiriting.';
  else if (s === null) e.start = "Vaqt noto'g'ri.";
  if (!end) e.end = 'Ketish vaqtini kiriting.';
  else if (en === null) e.end = "Vaqt noto'g'ri.";
  if (s !== null && en !== null) {
    if (en <= s) e.end = "Ketish vaqti kelish vaqtidan keyin bo'lishi kerak.";
    else if (en - s < MIN_WORK_MINUTES) e.end = "Ish vaqti kamida 1 soat bo'lishi kerak.";
  }
  return e;
}

/** `effectiveFrom` bugundan keyin bo'lsa — ertadan kuchga kiradigan (kutilayotgan) o'zgarish. */
export function isWorkHoursPending(wh: StudentWorkHoursDto, today = tashkentToday()): boolean {
  return wh.effectiveFrom !== null && wh.effectiveFrom > today;
}

/** "09:00", "17:00" → "09:00–17:00"; biri yo'q → null. */
export function formatHoursRange(start: string | null, end: string | null): string | null {
  return start && end ? `${start.slice(0, 5)}–${end.slice(0, 5)}` : null;
}

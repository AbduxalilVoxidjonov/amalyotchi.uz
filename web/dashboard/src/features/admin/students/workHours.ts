import type { StudentWorkHours } from './types';

/** Ish vaqtining minimal davomiyligi (daqiqa) — backend `StudentProfile.MinWorkMinutes` bilan bir xil. */
export const MIN_WORK_MINUTES = 60;

export type WorkHoursErrors = Partial<Record<'start' | 'end', string>>;

/** "09:30" | "09:30:00" → 570; noto'g'ri → null. */
export function timeToMinutes(value: string): number | null {
  const m = /^(\d{2}):(\d{2})/.exec(value);
  if (!m) return null;
  const h = Number(m[1]);
  const min = Number(m[2]);
  return h < 24 && min < 60 ? h * 60 + min : null;
}

/** "HH:mm:ss" → "HH:mm" (input[type=time] ba'zan soniya bilan qaytaradi). */
export const hhmm = (v: string) => v.slice(0, 5);

/** Server qoidalarining klient nusxasi: ikkalasi kiritilgan, ketish > kelish, kamida 1 soat. */
export function validateWorkHours(start: string, end: string): WorkHoursErrors {
  const e: WorkHoursErrors = {};
  const s = start ? timeToMinutes(start) : null;
  const en = end ? timeToMinutes(end) : null;
  if (!start) e.start = 'Kelish vaqtini kiriting.';
  else if (s === null) e.start = 'Vaqtni HH:mm formatida kiriting.';
  if (!end) e.end = 'Ketish vaqtini kiriting.';
  else if (en === null) e.end = 'Vaqtni HH:mm formatida kiriting.';
  if (s !== null && en !== null) {
    if (en <= s) e.end = "Ketish vaqti kelish vaqtidan keyin bo'lishi kerak.";
    else if (en - s < MIN_WORK_MINUTES) e.end = "Ish vaqti kamida 1 soat bo'lishi kerak.";
  }
  return e;
}

/** "09:00", "17:00" → "09:00–17:00"; biri yo'q → null. */
export function formatHoursRange(start: string | null, end: string | null): string | null {
  return start && end ? `${hhmm(start)}–${hhmm(end)}` : null;
}

/** `effectiveFrom` bugundan keyin — ertadan kuchga kiradigan (kutilayotgan) o'zgarish. */
export function isWorkHoursPending(wh: StudentWorkHours, today: string): boolean {
  return wh.effectiveFrom !== null && wh.effectiveFrom > today;
}

/** "2026-10-03" + 1 kun → "2026-10-04" (UTC hisobida, vaqt mintaqasidan mustaqil). */
export function addDays(dateOnly: string, days: number): string {
  const d = new Date(`${dateOnly}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

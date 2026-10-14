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

/** `effectiveFrom` bugundan keyin bo'lsa — ertadan kuchga kiradigan (kutilayotgan) o'zgarish. */
export function isWorkHoursPending(wh: StudentWorkHoursDto, today = tashkentToday()): boolean {
  return wh.effectiveFrom !== null && wh.effectiveFrom > today;
}

/** "09:00", "17:00" → "09:00–17:00"; biri yo'q → null. */
export function formatHoursRange(start: string | null, end: string | null): string | null {
  return start && end ? `${start.slice(0, 5)}–${end.slice(0, 5)}` : null;
}

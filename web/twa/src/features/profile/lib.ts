import type { StudentProfileDto, StudentProfilePracticeDto } from './types';

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

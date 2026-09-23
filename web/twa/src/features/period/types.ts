/**
 * Kontrakt v3.5 §4.6 — bir guruhda bir nechta amaliyot davri (kuzgi, bahorgi).
 * `StudentPeriodOption` — `today.period`, `portfolio.periods` da bir xil shakl.
 */
export type StudentPeriodStatus = 'planned' | 'active' | 'closed';

export interface StudentPeriodOption {
  id: string;
  name: string;
  /** DateOnly */
  startDate: string;
  endDate: string;
  /**
   * Hisoblangan holat. Tugagan, lekin yopilmagan davr `active` bo'lib qoladi —
   * "tugagan"ni `endDate < bugun` dan chiqaramiz (`periodPhase`).
   */
  status: StudentPeriodStatus;
  /** `periodId` berilmaganda tanlanadigan (sukut) davr. */
  isDefault: boolean;
}

/** Davr bugungi sanaga nisbatan: hali boshlanmagan · davom etmoqda · tugagan. */
export type PeriodPhase = 'upcoming' | 'ongoing' | 'ended';

/** `today` — DateOnly "2026-12-20" (server sanasi, `TodayDto.date`). */
export function periodPhase(period: StudentPeriodOption, today: string): PeriodPhase {
  if (period.status === 'planned' || period.startDate > today) return 'upcoming';
  if (period.status === 'closed' || period.endDate < today) return 'ended';
  return 'ongoing';
}

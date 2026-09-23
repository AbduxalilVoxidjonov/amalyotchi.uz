import type { StudentPeriodOption } from '@/features/period/types';

/** `PortfolioScoreDto.key`: attendance · reports · tutor · reference. */
export type PortfolioScoreKey = 'attendance' | 'reports' | 'tutor' | 'reference';

export interface PortfolioScoreDto {
  key: PortfolioScoreKey | (string & {});
  weightPct: number;
  points: number;
}

export interface PortfolioStatsDto {
  attendancePct: number;
  daysPresent: number;
  daysTotal: number;
  late: number;
  excused: number;
  reports: number;
  avgScore: number;
}

export interface PortfolioConclusionDto {
  text: string;
  author: string;
  /** ISO datetime */
  date: string;
}

/**
 * GET /api/student/portfolio?periodId= (`PortfolioDto`). Davr yo'q yoki begona `periodId` → 404.
 * `periodId` berilmasa — sukut davr (§4.6 "default": davom etayotgan → oxirgi tugagan → kelgusi).
 */
export interface PortfolioDto {
  student: string;
  group: string;
  /** "Ishlab chiqarish amaliyoti 2026" */
  practiceTitle: string;
  /** null — ariza tasdiqlanmagan */
  company: string | null;
  /** DateOnly */
  periodFrom: string;
  periodTo: string;
  stats: PortfolioStatsDto;
  /** Yakuniy baho tarkibi — tartib bilan. */
  score: PortfolioScoreDto[];
  /** Jami ball (100 dan) */
  total: number;
  /** 2–5; null — hali yakunlanmagan */
  grade: number | null;
  /** Tyutor yakunlagan (baho qat'iy). */
  finalized: boolean;
  conclusion: PortfolioConclusionDto | null;
  /** PDF havolasi (WebApp.openLink) | null — hali tayyor emas */
  pdfUrl: string | null;
  /** v3.5 — javob shu davr bo'yicha. */
  periodId: string;
  /** v3.5 — talabaning barcha davrlari (tarix tanlagichi), `startDate` kamayish tartibida. */
  periods: StudentPeriodOption[];
}

export const PORTFOLIO_SCORE_LABEL: Record<PortfolioScoreKey, string> = {
  attendance: 'Davomat',
  reports: 'Kundalik hisobotlar',
  tutor: 'Tyutor bahosi',
  reference: 'Korxona tavsifnomasi',
};

export function scoreLabel(key: string): string {
  return (PORTFOLIO_SCORE_LABEL as Record<string, string>)[key] ?? key;
}

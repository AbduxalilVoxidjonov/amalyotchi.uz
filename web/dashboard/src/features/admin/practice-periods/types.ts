import type { StatusKind } from '@/shared/ui';
import type { ApplicationStatus } from '../companies/types';

/**
 * Admin · Amaliyot davrlari — backend kontrakti (practice-periods-contract.md) bilan AYNAN.
 * JSON camelCase; sanalar ISO `YYYY-MM-DD`, vaqt `HH:mm`.
 */
export type PracticePeriodStatus = 'planned' | 'active' | 'closed';

export interface PracticePeriodListItem {
  id: string;
  name: string;
  /** YYYY-MM-DD */
  startDate: string;
  endDate: string;
  status: PracticePeriodStatus;
  groupsCount: number;
  /** Biriktirilgan guruhlardagi faol talabalar. */
  studentsCount: number;
  /** ISO datetime. */
  createdAt: string;
}

export interface PracticePeriodGroup {
  /** StudentGroup id. */
  id: string;
  /** "412-22" */
  code: string;
  course: number;
  studentsCount: number;
  facultyId: string;
  facultyName: string;
  departmentId: string;
  departmentName: string;
  directionId: string;
  directionName: string;
}

export interface PracticePeriodDetail extends PracticePeriodListItem {
  /** "09:00" (yaratilishda global sozlamadan nusxa). */
  dailyStart: string;
  /** "17:00" */
  dailyEnd: string;
  /** "1,2,3,4,5,6" (ISO hafta kunlari). */
  workDays: string;
  requiredDays: number;
  dailyReportRequired: boolean;
  groups: PracticePeriodGroup[];
}

/** Davr jadvali: kunlik ish vaqti va ish kunlari (backend'da ixtiyoriy, frontend doim yuboradi). */
export interface PracticePeriodSchedule {
  /** "HH:mm" */
  dailyStart: string;
  /** "HH:mm", `dailyStart` dan keyin. */
  dailyEnd: string;
  /** Tartiblangan CSV, 1 — Du … 7 — Ya ("1,2,3,4,5"). */
  workDays: string;
}

export interface PracticePeriodCreate extends PracticePeriodSchedule {
  name: string;
  startDate: string;
  endDate: string;
  groupIds: string[];
}

/** Yopilgan davr → 409. `workDays`/sanalar o'zgarsa backend `requiredDays` ni qayta hisoblaydi. */
export interface PracticePeriodUpdate extends PracticePeriodSchedule {
  name: string;
  startDate: string;
  endDate: string;
}

/** To'liq ro'yxat (set semantikasi). */
export interface PracticePeriodGroupsUpdate {
  groupIds: string[];
}

export const PERIOD_STATUS_LABEL: Record<
  PracticePeriodStatus,
  { label: string; kind: StatusKind }
> = {
  planned: { label: 'Rejada', kind: 'info' },
  active: { label: 'Faol', kind: 'ok' },
  closed: { label: 'Yopilgan', kind: 'neu' },
};

export const PERIOD_STATUSES: readonly PracticePeriodStatus[] = ['planned', 'active', 'closed'];

export function isPeriodStatus(v: string | null): v is PracticePeriodStatus {
  return v === 'planned' || v === 'active' || v === 'closed';
}

/* ────────────────────────────────────────────────────────────────────────────
 * Davr ko'rsatkichlari (statistika) — backend `PracticePeriodStatsContracts`.
 * GET /api/admin/practice-periods/{id}/stats
 * GET /api/admin/practice-periods/{id}/groups/{groupId}/students
 * ──────────────────────────────────────────────────────────────────────────── */

/** Baholar taqsimoti: 5 / 4 / 3 / 2 / qayta topshiradi. */
export interface GradeDistribution {
  excellent: number;
  good: number;
  satisfactory: number;
  unsatisfactory: number;
  retake: number;
}

/** Guruh (yoki butun davr) bo'yicha jamlangan ko'rsatkichlar. */
export interface GroupMetrics {
  studentsCount: number;
  /** Butun son, o'rtacha davomat foizi. */
  attendancePct: number;
  /** Davomati 70% dan past talabalar. */
  lowAttendanceCount: number;
  suspiciousDays: number;
  withCompanyCount: number;
  pendingApplicationsCount: number;
  diaryCount: number;
  diaryApprovedCount: number;
  /** 0..5; kundalik yo'q → 0. */
  diaryAvgScore: number;
  /** 0..100; hali ball yo'q → null. */
  avgTotal: number | null;
  finalizedCount: number;
  grades: GradeDistribution;
}

export interface PeriodGroupStats extends GroupMetrics {
  groupId: string;
  code: string;
  course: number;
  directionName: string;
}

export interface PracticePeriodStats {
  periodId: string;
  elapsedWorkDays: number;
  requiredDays: number;
  totals: GroupMetrics;
  groups: PeriodGroupStats[];
}

export interface PeriodGroupStudent {
  id: string;
  fullName: string;
  hemisId: string;
  company: string | null;
  applicationStatus: ApplicationStatus | null;
  attendancePct: number;
  presentDays: number;
  lateDays: number;
  absentDays: number;
  excusedDays: number;
  suspiciousDays: number;
  diaryCount: number;
  diaryAvg: number;
  /** 0..40 */
  attendancePoints: number;
  /** 0..30 */
  reportPoints: number;
  /** 0..20; hali qo'yilmagan → null. */
  tutorPoints: number | null;
  /** 0..10; hali qo'yilmagan → null. */
  referencePoints: number | null;
  total: number;
  /** 2..5; null — qayta topshiradi. */
  grade: number | null;
  finalized: boolean;
}

export interface PeriodGroupStudents {
  period: Pick<PracticePeriodListItem, 'id' | 'name' | 'status' | 'startDate' | 'endDate'>;
  group: { id: string; code: string; course: number; facultyName: string; directionName: string };
  elapsedWorkDays: number;
  metrics: GroupMetrics;
  students: PeriodGroupStudent[];
}

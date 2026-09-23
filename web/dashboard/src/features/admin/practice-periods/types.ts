import type { StatusKind } from '@/shared/ui';

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

export interface PracticePeriodCreate {
  name: string;
  startDate: string;
  endDate: string;
  groupIds: string[];
}

export interface PracticePeriodUpdate {
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

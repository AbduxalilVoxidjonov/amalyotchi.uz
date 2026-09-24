import { PERIOD_SEARCH_PARAM } from '@/features/tutor/students/periods';
import type { PracticePeriodListItem } from './types';

export const PRACTICE_PERIODS_HREF = '/admin/practice-periods';
export const NEW_PERIOD_HREF = '/admin/practice-periods/new';

export const periodHref = (r: Pick<PracticePeriodListItem, 'id'>) =>
  `${PRACTICE_PERIODS_HREF}/${r.id}`;

/** Davr ichidagi guruh sahifasi: talabalar va ko'rsatkichlar. */
export const periodGroupHref = (periodId: string, groupId: string) =>
  `${PRACTICE_PERIODS_HREF}/${periodId}/groups/${groupId}`;

/** Admin talaba profili — shu davr tanlangan holda (`?period=`, `usePeriodParam`). */
export const periodStudentHref = (periodId: string, studentId: string) =>
  `/admin/students/${studentId}?${PERIOD_SEARCH_PARAM}=${encodeURIComponent(periodId)}`;

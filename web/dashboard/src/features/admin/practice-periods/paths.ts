import type { PracticePeriodListItem } from './types';

export const PRACTICE_PERIODS_HREF = '/admin/practice-periods';
export const NEW_PERIOD_HREF = '/admin/practice-periods/new';

export const periodHref = (r: Pick<PracticePeriodListItem, 'id'>) =>
  `${PRACTICE_PERIODS_HREF}/${r.id}`;

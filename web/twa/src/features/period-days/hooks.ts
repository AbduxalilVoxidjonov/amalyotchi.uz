import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { periodDaysApi } from './api';

export const periodDaysKeys = {
  all: ['student', 'period-days'] as const,
  period: (periodId: string | null) =>
    ['student', 'period-days', { periodId: periodId ?? 'default' }] as const,
};

export function usePeriodDaysQuery(periodId: string | null) {
  return useQuery({
    queryKey: periodDaysKeys.period(periodId),
    queryFn: ({ signal }) => periodDaysApi.get(periodId, signal),
    // Davr almashganda eski ro'yxat qoladi — ekran "sakramaydi".
    placeholderData: keepPreviousData,
  });
}

import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { calendarApi } from './api';

export const calendarKeys = {
  all: ['student', 'calendar'] as const,
  month: (month: string | null) => ['student', 'calendar', { month: month ?? 'current' }] as const,
};

export function useCalendarMonthQuery(month: string | null) {
  return useQuery({
    queryKey: calendarKeys.month(month),
    queryFn: ({ signal }) => calendarApi.month(month, signal),
    // Oy almashganda eski grid qoladi — "sakrash" bo'lmaydi.
    placeholderData: keepPreviousData,
  });
}

import { useQuery } from '@tanstack/react-query';
import { tutorKeys } from '../query-keys';
import { calendarApi } from './api';

export function useCalendarQuery(params: { month: string }) {
  return useQuery({
    queryKey: tutorKeys.calendar(params),
    queryFn: () => calendarApi.get(params),
    placeholderData: (prev) => prev,
  });
}

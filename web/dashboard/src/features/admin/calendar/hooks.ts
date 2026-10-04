import { useQuery } from '@tanstack/react-query';
import { opsKeys } from '../shared/opsKeys';
import { calendarApi } from './api';

export function useCalendarQuery(params: { month: string }) {
  return useQuery({
    queryKey: opsKeys.calendar(params),
    queryFn: () => calendarApi.get(params),
    placeholderData: (prev) => prev,
  });
}

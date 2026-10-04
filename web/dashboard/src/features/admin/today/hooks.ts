import { useQuery } from '@tanstack/react-query';
import { tutorKeys } from '../query-keys';
import { todayApi, type TodayParams } from './api';

export function useTodayQuery(params: TodayParams) {
  return useQuery({
    queryKey: tutorKeys.today(params),
    queryFn: () => todayApi.get(params),
    placeholderData: (prev) => prev,
    staleTime: 30 * 1000,
  });
}

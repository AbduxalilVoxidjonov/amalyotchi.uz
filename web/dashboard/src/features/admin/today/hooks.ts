import { useQuery } from '@tanstack/react-query';
import { opsKeys } from '../shared/opsKeys';
import { todayApi, type TodayParams } from './api';

export function useTodayQuery(params: TodayParams) {
  return useQuery({
    queryKey: opsKeys.today(params),
    queryFn: () => todayApi.get(params),
    placeholderData: (prev) => prev,
    staleTime: 30 * 1000,
  });
}

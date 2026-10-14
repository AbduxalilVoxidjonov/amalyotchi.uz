import { useQuery } from '@tanstack/react-query';
import { opsKeys } from '../shared/opsKeys';
import { todayApi, type TodayParams } from './api';

export const TODAY_REFETCH_MS = 60 * 1000;

export function useTodayQuery(params: TodayParams) {
  return useQuery({
    queryKey: opsKeys.today(params),
    queryFn: () => todayApi.get(params),
    placeholderData: (prev) => prev,
    staleTime: 30 * 1000,
    // `presence` (jonli joylashuv) eskirmasin; fon tab'da so'ramaydi (TanStack sukuti).
    refetchInterval: TODAY_REFETCH_MS,
  });
}

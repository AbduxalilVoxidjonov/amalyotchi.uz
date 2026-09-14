import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { haptic } from '@/shared/auth/telegram';
import { getCurrentPosition } from '@/shared/lib/geolocation';
import { todayApi } from './api';
import type { TodayDto } from './types';

export const todayKeys = {
  all: ['student', 'today'] as const,
};

export function useTodayQuery() {
  return useQuery({
    queryKey: todayKeys.all,
    queryFn: ({ signal }) => todayApi.get(signal),
    // Oyna/holat vaqtga bog'liq — 1 daqiqada yangilanadi.
    refetchInterval: 60_000,
  });
}

/**
 * KELDIM ⇄ KETDIM. Avval qurilma joylashuvi (GeoError bo'lishi mumkin), keyin server.
 * Server yangi TodayDto qaytaradi — keshga to'g'ridan-to'g'ri yoziladi (qayta so'rovsiz).
 * TODO(offline): tarmoq xatosida (ApiError.kind === 'network') urinishni navbatga qo'yish.
 */
export function useToggleCheckin() {
  const qc = useQueryClient();
  return useMutation({
    mutationKey: ['student', 'checkin'],
    mutationFn: async (mode: 'checkin' | 'checkout'): Promise<TodayDto> => {
      const point = await getCurrentPosition();
      return mode === 'checkin' ? todayApi.checkin(point) : todayApi.checkout(point);
    },
    onSuccess: (data) => {
      haptic('success');
      qc.setQueryData(todayKeys.all, data);
      // Kalendar/portfolio davomati o'zgargan bo'lishi mumkin.
      void qc.invalidateQueries({ queryKey: ['student', 'calendar'] });
      void qc.invalidateQueries({ queryKey: ['student', 'portfolio'] });
    },
    onError: () => haptic('error'),
  });
}

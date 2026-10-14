import { useQuery } from '@tanstack/react-query';
import { opsKeys } from '../shared/opsKeys';
import { locationApi } from './api';

/** Bugungi iz jonli — 60 s da yangilanadi; o'tgan kunlar o'zgarmaydi. */
export function useLocationPingsQuery(studentId: string, date: string, isToday: boolean) {
  return useQuery({
    queryKey: opsKeys.students.locationPings(studentId, date),
    queryFn: () => locationApi.pings(studentId, date),
    enabled: studentId !== '',
    staleTime: isToday ? 30 * 1000 : 5 * 60 * 1000,
    refetchInterval: isToday ? 60 * 1000 : false,
  });
}

import { useQuery } from '@tanstack/react-query';
import { adminKeys } from '../shared/keys';
import { dashboardApi } from './api';

export function useAdminDashboardQuery() {
  return useQuery({
    queryKey: adminKeys.dashboard(),
    queryFn: dashboardApi.get,
    staleTime: 60 * 1000,
  });
}

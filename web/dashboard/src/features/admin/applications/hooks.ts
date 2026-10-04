import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { navKeys } from '@/app/nav-keys';
import { opsKeys } from '../shared/opsKeys';
import { applicationsApi } from './api';
import type { ApplicationDecisionRequest, ApplicationTab } from './types';

export function useApplicationsQuery(params: { tab: ApplicationTab }) {
  return useQuery({
    queryKey: opsKeys.applications.list(params),
    queryFn: () => applicationsApi.list(params),
    placeholderData: (prev) => prev,
  });
}

export function useApplicationDetailQuery(id: string | null) {
  return useQuery({
    queryKey: opsKeys.applications.detail(id ?? ''),
    queryFn: () => applicationsApi.detail(id!),
    enabled: id !== null,
  });
}

/** Qaror → ro'yxat + detail invalidatsiya + sidebar nav badge'lari. */
export function useApplicationDecision() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ['admin', 'applications', 'decision'],
    mutationFn: ({ id, body }: { id: string; body: ApplicationDecisionRequest }) =>
      applicationsApi.decide(id, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: opsKeys.applications.all });
      // Sidebar "Arizalar" badge'i (yangi arizalar soni).
      void queryClient.invalidateQueries({ queryKey: navKeys.all });
    },
  });
}

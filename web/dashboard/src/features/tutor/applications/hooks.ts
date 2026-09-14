import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { tutorKeys } from '../query-keys';
import { applicationsApi } from './api';
import type { ApplicationDecisionRequest, ApplicationTab } from './types';

export function useApplicationsQuery(params: { tab: ApplicationTab }) {
  return useQuery({
    queryKey: tutorKeys.applications.list(params),
    queryFn: () => applicationsApi.list(params),
    placeholderData: (prev) => prev,
  });
}

export function useApplicationDetailQuery(id: string | null) {
  return useQuery({
    queryKey: tutorKeys.applications.detail(id ?? ''),
    queryFn: () => applicationsApi.detail(id!),
    enabled: id !== null,
  });
}

/** Qaror → ro'yxat + detail invalidatsiya (nav badge ham shu kalitdan olinadi keyinchalik). */
export function useApplicationDecision() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ['tutor', 'applications', 'decision'],
    mutationFn: ({ id, body }: { id: string; body: ApplicationDecisionRequest }) =>
      applicationsApi.decide(id, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: tutorKeys.applications.all }),
  });
}

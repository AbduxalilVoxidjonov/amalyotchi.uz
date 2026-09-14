import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { tutorKeys } from '../query-keys';
import { leaveRequestsApi } from './api';
import type { LeaveDecision } from './types';

export function useLeaveRequestsQuery() {
  return useQuery({ queryKey: tutorKeys.leaveRequests.list(), queryFn: () => leaveRequestsApi.list() });
}

/** Bitta yoki bir nechta so'rovga qaror (Hammasini tasdiqlash → ids[]). Xato bo'lsa ham ro'yxat yangilanadi. */
export function useLeaveDecision() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ['tutor', 'leave-requests', 'decision'],
    mutationFn: async ({ ids, decision }: { ids: string[]; decision: LeaveDecision }) =>
      Promise.all(ids.map((id) => leaveRequestsApi.decide(id, { decision }))),
    onSettled: () => queryClient.invalidateQueries({ queryKey: tutorKeys.leaveRequests.all }),
  });
}

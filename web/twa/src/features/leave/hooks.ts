import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { leaveApi } from './api';
import type { LeaveRequestDto } from './types';

export const leaveKeys = {
  all: ['student', 'leave'] as const,
  list: () => ['student', 'leave', 'list'] as const,
};

export function useLeaveRequestsQuery() {
  return useQuery({ queryKey: leaveKeys.list(), queryFn: ({ signal }) => leaveApi.list(signal) });
}

export function useCreateLeaveRequest() {
  const qc = useQueryClient();
  return useMutation({
    mutationKey: ['student', 'leave', 'create'],
    mutationFn: leaveApi.create,
    onSuccess: (created) => {
      qc.setQueryData<LeaveRequestDto[]>(leaveKeys.list(), (prev) =>
        prev ? [created, ...prev] : [created],
      );
      void qc.invalidateQueries({ queryKey: ['student', 'calendar'] });
    },
  });
}

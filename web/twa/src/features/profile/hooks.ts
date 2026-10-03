import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { todayKeys } from '@/features/today/hooks';
import { profileApi } from './api';
import type { StudentProfileDto, UpdateWorkHoursRequest } from './types';

export const profileKeys = {
  all: ['student', 'profile'] as const,
  workHours: ['student', 'profile', 'work-hours'] as const,
};

export function useProfileQuery() {
  return useQuery({
    queryKey: profileKeys.all,
    queryFn: ({ signal }) => profileApi.get(signal),
  });
}

/**
 * Ish vaqtini saqlash / davr vaqtiga qaytarish (ikkalasi null). Javob (yangi workHours) profil keshiga
 * darhol yoziladi; profil va bugungi holat (bosh ekran oynasi) qayta so'raladi.
 */
export function useUpdateWorkHours() {
  const qc = useQueryClient();
  return useMutation({
    mutationKey: profileKeys.workHours,
    mutationFn: (body: UpdateWorkHoursRequest) => profileApi.updateWorkHours(body),
    onSuccess: (workHours) => {
      qc.setQueryData<StudentProfileDto>(profileKeys.all, (p) => (p ? { ...p, workHours } : p));
      void qc.invalidateQueries({ queryKey: profileKeys.all });
      void qc.invalidateQueries({ queryKey: todayKeys.all });
    },
  });
}

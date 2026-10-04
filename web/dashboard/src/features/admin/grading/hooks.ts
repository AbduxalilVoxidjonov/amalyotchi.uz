import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { tutorKeys } from '../query-keys';
import { gradingApi } from './api';
import type { GradingUpdateRequest } from './types';

export function useGradingQuery() {
  return useQuery({ queryKey: tutorKeys.grading.list(), queryFn: gradingApi.list });
}

/** Bir nechta talabani birdan yangilash (Tavsiya etilgan ballarni qabul qilish). */
export function useGradingUpdate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ['tutor', 'grading', 'update'],
    mutationFn: (items: { studentId: string; body: GradingUpdateRequest }[]) =>
      Promise.all(items.map((it) => gradingApi.update(it.studentId, it.body))),
    onSettled: () => queryClient.invalidateQueries({ queryKey: tutorKeys.grading.all }),
  });
}

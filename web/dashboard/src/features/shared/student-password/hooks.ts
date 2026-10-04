import { useMutation, useQueryClient } from '@tanstack/react-query';
import { studentPasswordApi } from './api';

/**
 * Talaba parolini o'rnatish. Muvaffaqiyatda profil (`hasPassword`) qayta so'raladi —
 * barcha davrlar kaliti (`['admin','student',id,*]`).
 */
export function useSetStudentPassword() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ['admin', 'students', 'set-password'],
    mutationFn: ({ id, password }: { id: string; password: string }) =>
      studentPasswordApi.set(id, password),
    onSuccess: (_data, { id }) => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'student', id] });
    },
  });
}

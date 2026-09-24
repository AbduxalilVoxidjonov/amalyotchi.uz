import { useMutation, useQueryClient } from '@tanstack/react-query';
import { studentPasswordApi, type StudentPasswordArea } from './api';

/**
 * Talaba parolini o'rnatish. Muvaffaqiyatda profil (`hasPassword`) qayta so'raladi —
 * barcha davrlar kaliti (`['admin','student',id,*]` / `['tutor','students','detail',id,*]`).
 */
export function useSetStudentPassword(area: StudentPasswordArea) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [area, 'students', 'set-password'],
    mutationFn: ({ id, password }: { id: string; password: string }) =>
      studentPasswordApi.set(area, id, password),
    onSuccess: (_data, { id }) => {
      void queryClient.invalidateQueries({
        queryKey: area === 'admin' ? ['admin', 'student', id] : ['tutor', 'students', 'detail', id],
      });
    },
  });
}

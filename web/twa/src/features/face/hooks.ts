import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { haptic } from '@/shared/auth/telegram';
import { todayKeys } from '@/features/today/hooks';
import { faceApi } from './api';
import { needsFaceEnrollment } from './types';

export const faceKeys = {
  all: ['student', 'face'] as const,
};

export function useStudentFaceQuery() {
  return useQuery({
    queryKey: faceKeys.all,
    queryFn: ({ signal }) => faceApi.get(signal),
    // Tyutor qarori (pending → approved/rejected) ilova ochiq turganda ham ko'rinsin.
    refetchInterval: (query) => (query.state.data?.status === 'pending' ? 60_000 : false),
  });
}

/**
 * Darvoza holati (RootLayout): `pending` — birinchi javob kutilmoqda; `needsEnrollment` — `/face` ga
 * yo'naltirish kerak. So'rov xatosi (eski server 404, tarmoq) — darvoza yopilmaydi (ilova ochiladi).
 */
export function useFaceGate() {
  const q = useStudentFaceQuery();
  return { pending: q.isPending, needsEnrollment: needsFaceEnrollment(q.data) };
}

/** Etalon yuborish → javob (pending) keshga yoziladi; bugungi holat ham yangilanadi. */
export function useSubmitFace() {
  const qc = useQueryClient();
  return useMutation({
    mutationKey: ['student', 'face', 'submit'],
    mutationFn: faceApi.submit,
    onSuccess: (data) => {
      haptic('success');
      qc.setQueryData(faceKeys.all, data);
      void qc.invalidateQueries({ queryKey: todayKeys.all });
    },
    onError: () => haptic('error'),
  });
}

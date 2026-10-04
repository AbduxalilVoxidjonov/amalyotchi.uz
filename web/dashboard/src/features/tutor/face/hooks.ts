import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { navKeys } from '@/app/nav-keys';
import { tutorKeys } from '../query-keys';
import { faceApi } from './api';
import type { FaceEnrollmentTab } from './types';

export function useFaceEnrollmentsQuery(params: { status: FaceEnrollmentTab }) {
  return useQuery({
    queryKey: tutorKeys.face.list(params),
    queryFn: () => faceApi.list(params),
    placeholderData: (prev) => prev,
  });
}

export type FaceAction =
  | { kind: 'approve'; studentId: string }
  | { kind: 'reject'; studentId: string; reason: string }
  | { kind: 'reset'; studentId: string };

/**
 * Tasdiqlash / rad etish / bekor qilish → ro'yxat, talaba profili (`detail.face`) va sidebar
 * "Yuz tasdiqlash" badge'i (`pendingFaceEnrollments`) yangilanadi.
 */
export function useFaceAction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ['tutor', 'face', 'action'],
    mutationFn: (action: FaceAction) => {
      switch (action.kind) {
        case 'approve':
          return faceApi.approve(action.studentId);
        case 'reject':
          return faceApi.reject(action.studentId, action.reason);
        case 'reset':
          return faceApi.reset(action.studentId);
      }
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: tutorKeys.face.all });
      void queryClient.invalidateQueries({ queryKey: tutorKeys.students.all });
      void queryClient.invalidateQueries({ queryKey: navKeys.all });
    },
  });
}

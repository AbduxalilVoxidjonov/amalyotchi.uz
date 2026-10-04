import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { navKeys } from '@/app/nav-keys';
import { opsKeys } from '../shared/opsKeys';
import { diariesApi } from './api';
import type { DiaryReviewRequest } from './types';

export function useDiariesQuery() {
  return useQuery({ queryKey: opsKeys.diaries.list(), queryFn: () => diariesApi.list() });
}

/**
 * Kundalikni ko'rib chiqish. Ball kundaliklar ro'yxatiga ham, talaba profiliga ham (davomat
 * qatoridagi kundalik, kundalik statistikasi va yakuniy baho) ta'sir qiladi — shuning uchun
 * profil kalitlari ham yangilanadi.
 */
export function useDiaryReview() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ['admin', 'diaries', 'review'],
    mutationFn: ({ id, body }: { id: string; body: DiaryReviewRequest }) =>
      diariesApi.review(id, body),
    onSuccess: () => {
      for (const queryKey of [
        opsKeys.diaries.all,
        navKeys.all,
        ['admin', 'students'],
        ['admin', 'student'],
      ]) {
        void queryClient.invalidateQueries({ queryKey });
      }
    },
  });
}

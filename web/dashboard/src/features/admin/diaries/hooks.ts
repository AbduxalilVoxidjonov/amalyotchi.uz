import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { navKeys } from '@/app/nav-keys';
import { opsKeys } from '../shared/opsKeys';
import { diariesApi } from './api';
import type { DiaryReviewRequest } from './types';

/** Bir "sahifa" — backend sukut limiti bilan bir xil. "Ko'proq" har bosilganda limit shuncha oshadi. */
export const DIARIES_PAGE_SIZE = 100;

/**
 * Kundaliklar navbati — butun tarix emas, birinchi `limit` ta (tekshirilmaganlar birinchi). Avval
 * hamma yozuv bitta javobda kelardi (yuzlab talabada bir necha MB, minglab karta). Limit oshganda
 * oldingi ro'yxat ko'rinib turadi (`keepPreviousData`).
 */
export function useDiariesQuery(limit: number = DIARIES_PAGE_SIZE) {
  return useQuery({
    queryKey: [...opsKeys.diaries.list(), limit] as const,
    queryFn: () => diariesApi.list({ limit }),
    placeholderData: keepPreviousData,
  });
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

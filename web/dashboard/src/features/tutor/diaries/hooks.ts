import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { navKeys } from '@/app/nav-keys';
import { tutorKeys } from '../query-keys';
import type { StudentApiArea } from '../students/types';
import { diariesApi } from './api';
import type { DiaryReviewRequest } from './types';

export function useDiariesQuery() {
  return useQuery({ queryKey: tutorKeys.diaries.list(), queryFn: () => diariesApi.list() });
}

/**
 * Kundalikni ko'rib chiqish. `area` — qaysi rol endpoint'i (tyutor yoki admin paneli).
 * Ball kundaliklar ro'yxatiga ham, talaba profiliga ham (davomat qatoridagi kundalik,
 * kundalik statistikasi va yakuniy baho) ta'sir qiladi — shuning uchun ikkala rolning
 * profil kalitlari ham yangilanadi.
 */
export function useDiaryReview(area: StudentApiArea = 'tutor') {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [area, 'diaries', 'review'],
    mutationFn: ({ id, body }: { id: string; body: DiaryReviewRequest }) =>
      diariesApi.review(id, body, area),
    onSuccess: () => {
      for (const queryKey of [
        tutorKeys.diaries.all,
        navKeys.all,
        ['tutor', 'students'],
        ['admin', 'students'],
        ['admin', 'student'],
      ]) {
        void queryClient.invalidateQueries({ queryKey });
      }
    },
  });
}

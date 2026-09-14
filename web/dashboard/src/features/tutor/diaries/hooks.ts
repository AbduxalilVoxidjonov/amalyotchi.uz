import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { tutorKeys } from '../query-keys';
import { diariesApi } from './api';
import type { DiaryReviewRequest } from './types';

export function useDiariesQuery() {
  return useQuery({ queryKey: tutorKeys.diaries.list(), queryFn: () => diariesApi.list() });
}

export function useDiaryReview() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ['tutor', 'diaries', 'review'],
    mutationFn: ({ id, body }: { id: string; body: DiaryReviewRequest }) =>
      diariesApi.review(id, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: tutorKeys.diaries.all }),
  });
}

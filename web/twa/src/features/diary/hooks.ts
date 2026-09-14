import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { todayKeys } from '@/features/today/hooks';
import { diaryApi } from './api';
import type { DiaryEntryDto } from './types';

export const diaryKeys = {
  all: ['student', 'diary'] as const,
  list: () => ['student', 'diary', 'list'] as const,
};

export function useDiaryQuery() {
  return useQuery({ queryKey: diaryKeys.list(), queryFn: ({ signal }) => diaryApi.list(signal) });
}

export function useCreateDiaryEntry() {
  const qc = useQueryClient();
  return useMutation({
    mutationKey: ['student', 'diary', 'create'],
    mutationFn: diaryApi.create,
    onSuccess: (entry) => {
      qc.setQueryData<DiaryEntryDto[]>(diaryKeys.list(), (prev) =>
        prev ? [entry, ...prev] : [entry],
      );
      // Bosh ekrandagi "Hisobotlar" va diary.submittedToday yangilanadi.
      void qc.invalidateQueries({ queryKey: todayKeys.all });
      void qc.invalidateQueries({ queryKey: ['student', 'portfolio'] });
    },
  });
}

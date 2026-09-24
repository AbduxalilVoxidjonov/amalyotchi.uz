import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { todayKeys } from '@/features/today/hooks';
import { diaryApi } from './api';
import type { DiaryEntryDto } from './types';

export const diaryKeys = {
  all: ['student', 'diary'] as const,
  list: () => ['student', 'diary', 'list'] as const,
};

export function useDiaryQuery({ enabled = true }: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: diaryKeys.list(),
    queryFn: ({ signal }) => diaryApi.list(signal),
    enabled,
  });
}

export function useCreateDiaryEntry() {
  const qc = useQueryClient();
  return useMutation({
    mutationKey: ['student', 'diary', 'create'],
    mutationFn: diaryApi.create,
    onSuccess: (entry) => {
      qc.setQueryData<DiaryEntryDto[]>(diaryKeys.list(), (prev) =>
        // Qayta yozish (`rewrite` → `submitted`) o'sha yozuvni qaytaradi — dublikat bo'lmasin.
        prev ? [entry, ...prev.filter((e) => e.id !== entry.id)] : [entry],
      );
      // Bosh ekrandagi "Hisobotlar" va diary.submittedToday yangilanadi.
      void qc.invalidateQueries({ queryKey: todayKeys.all });
      // Bosh ekran kunlar ro'yxatidagi kundalik holati.
      void qc.invalidateQueries({ queryKey: ['student', 'period-days'] });
    },
  });
}

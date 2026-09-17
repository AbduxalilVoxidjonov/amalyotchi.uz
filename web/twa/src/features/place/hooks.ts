import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { placeApi } from './api';
import type { PracticePlaceDto } from './types';

export const placeKeys = {
  all: ['student', 'place'] as const,
  lookup: ['companies', 'lookup'] as const,
  submit: ['student', 'place', 'submit'] as const,
};

export function usePlaceQuery() {
  return useQuery({ queryKey: placeKeys.all, queryFn: ({ signal }) => placeApi.get(signal) });
}

/**
 * STIR qidiruvi — foydalanuvchi "Qidirish" tugmasini bosganda ishga tushadi, shuning uchun
 * `useQuery` emas, `useMutation` (natija forma holatida saqlanadi).
 */
export function useCompanyLookup() {
  return useMutation({
    mutationKey: placeKeys.lookup,
    mutationFn: (tin: string) => placeApi.lookup(tin),
  });
}

/** Ariza yuborish: 201 dan keyin `place` so'rovi yangilanadi (endi "Tekshiruvda" ko'rinadi). */
export function useSubmitPlace() {
  const qc = useQueryClient();
  return useMutation({
    mutationKey: placeKeys.submit,
    mutationFn: (tin: string) => placeApi.submit({ tin }),
    onSuccess: (created) => {
      qc.setQueryData<PracticePlaceDto>(placeKeys.all, created);
      void qc.invalidateQueries({ queryKey: placeKeys.all });
      void qc.invalidateQueries({ queryKey: ['student', 'today'] });
    },
  });
}

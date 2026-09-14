import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { adminKeys } from '../shared/keys';
import type { ListParams } from '../shared/types';
import { tutorsApi } from './api';

export function useTutorsQuery(params: ListParams) {
  return useQuery({
    queryKey: adminKeys.tutors(params),
    queryFn: () => tutorsApi.list(params),
    placeholderData: keepPreviousData,
  });
}

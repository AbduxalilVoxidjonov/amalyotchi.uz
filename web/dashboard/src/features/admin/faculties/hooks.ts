import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { adminKeys } from '../shared/keys';
import type { ListParams } from '../shared/types';
import { facultiesApi } from './api';

export function useFacultiesQuery(params: ListParams) {
  return useQuery({
    queryKey: adminKeys.faculties(params),
    queryFn: () => facultiesApi.list(params),
    placeholderData: keepPreviousData,
  });
}

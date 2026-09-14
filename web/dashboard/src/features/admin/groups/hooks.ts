import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { adminKeys } from '../shared/keys';
import type { ListParams } from '../shared/types';
import { groupsApi } from './api';

export function useGroupsQuery(params: ListParams) {
  return useQuery({
    queryKey: adminKeys.groups(params),
    queryFn: () => groupsApi.list(params),
    placeholderData: keepPreviousData,
  });
}

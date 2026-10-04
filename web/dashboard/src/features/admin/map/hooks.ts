import { useQuery } from '@tanstack/react-query';
import { opsKeys } from '../shared/opsKeys';
import { mapApi } from './api';

export function useMapQuery() {
  return useQuery({ queryKey: opsKeys.map(), queryFn: () => mapApi.get(), staleTime: 30 * 1000 });
}

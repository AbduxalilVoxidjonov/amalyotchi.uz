import { useQuery } from '@tanstack/react-query';
import { tutorKeys } from '../query-keys';
import { mapApi } from './api';

export function useMapQuery() {
  return useQuery({ queryKey: tutorKeys.map(), queryFn: () => mapApi.get(), staleTime: 30 * 1000 });
}

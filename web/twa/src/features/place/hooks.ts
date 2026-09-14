import { useQuery } from '@tanstack/react-query';
import { placeApi } from './api';

export const placeKeys = {
  all: ['student', 'place'] as const,
};

export function usePlaceQuery() {
  return useQuery({ queryKey: placeKeys.all, queryFn: ({ signal }) => placeApi.get(signal) });
}

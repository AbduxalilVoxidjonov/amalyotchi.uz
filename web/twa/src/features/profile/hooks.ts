import { useQuery } from '@tanstack/react-query';
import { profileApi } from './api';

export const profileKeys = {
  all: ['student', 'profile'] as const,
};

export function useProfileQuery() {
  return useQuery({
    queryKey: profileKeys.all,
    queryFn: ({ signal }) => profileApi.get(signal),
  });
}

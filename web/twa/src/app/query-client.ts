import { QueryClient } from '@tanstack/react-query';
import { isApiError } from '@amaliyotchi/shared';

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30 * 1000,
        refetchOnWindowFocus: false,
        retry: (count, error) =>
          isApiError(error) && error.status > 0 && error.status < 500 ? false : count < 2,
      },
      mutations: { retry: false },
    },
  });
}

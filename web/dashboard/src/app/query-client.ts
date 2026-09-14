import { QueryClient } from '@tanstack/react-query';
import { isApiError } from '@amaliyotchi/shared';

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30 * 1000,
        refetchOnWindowFocus: false,
        retry: (failureCount, error) => {
          // 4xx xatolarni qayta urinish ma'nosiz; tarmoq/5xx — 2 marta.
          if (isApiError(error) && error.status > 0 && error.status < 500) return false;
          return failureCount < 2;
        },
      },
      mutations: { retry: false },
    },
  });
}

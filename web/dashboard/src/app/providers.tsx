import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { useState, type ReactNode } from 'react';
import { env } from '@/shared/lib/env';
import { createQueryClient } from './query-client';

interface AppProvidersProps {
  children: ReactNode;
  /** Testlar uchun tashqaridan berish mumkin. */
  queryClient?: QueryClient;
}

export function AppProviders({ children, queryClient }: AppProvidersProps) {
  const [client] = useState(() => queryClient ?? createQueryClient());
  return (
    <QueryClientProvider client={client}>
      {children}
      {env.isDev && !env.isTest && <ReactQueryDevtools initialIsOpen={false} />}
    </QueryClientProvider>
  );
}

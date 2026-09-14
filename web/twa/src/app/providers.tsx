import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';
import { createQueryClient } from './query-client';

export function AppProviders({
  children,
  queryClient,
}: {
  children: ReactNode;
  queryClient?: QueryClient;
}) {
  const [client] = useState(() => queryClient ?? createQueryClient());
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

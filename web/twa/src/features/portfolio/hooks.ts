import { useQuery } from '@tanstack/react-query';
import { portfolioApi } from './api';

export const portfolioKeys = {
  all: ['student', 'portfolio'] as const,
};

export function usePortfolioQuery() {
  return useQuery({
    queryKey: portfolioKeys.all,
    queryFn: ({ signal }) => portfolioApi.get(signal),
  });
}

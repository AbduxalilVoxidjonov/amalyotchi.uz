import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { portfolioApi } from './api';

export const portfolioKeys = {
  all: ['student', 'portfolio'] as const,
  /** `null` — sukut davr (backend `isDefault`). */
  detail: (periodId: string | null) => ['student', 'portfolio', periodId ?? 'default'] as const,
};

export function usePortfolioQuery(periodId: string | null = null) {
  return useQuery({
    queryKey: portfolioKeys.detail(periodId),
    queryFn: ({ signal }) => portfolioApi.get(periodId, signal),
    // Davr almashganda eski ma'lumot (va tanlagich) yangisi kelguncha ko'rinib turadi.
    placeholderData: keepPreviousData,
  });
}

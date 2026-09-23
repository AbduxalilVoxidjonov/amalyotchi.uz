import { api } from '@/shared/api/client';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import type { PortfolioDto } from './types';

export const portfolioApi = {
  /** `periodId` yo'q → sukut davr (backend tanlaydi). */
  get: (periodId: string | null, signal?: AbortSignal) =>
    api.get<PortfolioDto>(STUDENT_ENDPOINTS.portfolio, {
      ...(periodId ? { query: { periodId } } : {}),
      ...(signal ? { signal } : {}),
    }),
};

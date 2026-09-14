import { api } from '@/shared/api/client';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import type { PortfolioDto } from './types';

export const portfolioApi = {
  get: (signal?: AbortSignal) =>
    api.get<PortfolioDto>(STUDENT_ENDPOINTS.portfolio, signal ? { signal } : {}),
};

import { api } from '@/shared/api/client';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import type { StudentPeriodDays } from './types';

export const periodDaysApi = {
  /** `periodId` null → server sukut davrini (`isDefault`) qaytaradi. */
  get: (periodId: string | null, signal?: AbortSignal) =>
    api.get<StudentPeriodDays>(STUDENT_ENDPOINTS.periodDays, {
      query: { periodId: periodId ?? undefined },
      ...(signal ? { signal } : {}),
    }),
};

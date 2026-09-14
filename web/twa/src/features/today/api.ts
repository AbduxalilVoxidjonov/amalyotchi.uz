import { api } from '@/shared/api/client';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import type { CheckinRequest, TodayDto } from './types';

export const todayApi = {
  get: (signal?: AbortSignal) =>
    api.get<TodayDto>(STUDENT_ENDPOINTS.today, signal ? { signal } : {}),
  checkin: (body: CheckinRequest) => api.post<TodayDto>(STUDENT_ENDPOINTS.checkin, body),
  checkout: (body: CheckinRequest) => api.post<TodayDto>(STUDENT_ENDPOINTS.checkout, body),
};

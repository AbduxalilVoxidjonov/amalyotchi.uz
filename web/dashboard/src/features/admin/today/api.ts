import { api } from '@/shared/api';
import type { AttendanceFilter, TodayResponse } from './types';

export interface TodayParams {
  status: AttendanceFilter;
  page: number;
  q?: string | undefined;
}

/**
 * Tyutor · Bugun — `TutorTodayController`:
 *   GET /api/tutor/today?status=present|late|absent|excused|pending|suspicious&q=&page=&pageSize= → TodayResponse
 *   (`status` berilmasa — hammasi; noto'g'ri qiymat → 400.)
 */
export const todayApi = {
  get: (params: TodayParams) =>
    api.get<TodayResponse>('/api/tutor/today', {
      query: {
        status: params.status === 'all' ? undefined : params.status,
        q: params.q || undefined,
        page: params.page,
      },
    }),
};

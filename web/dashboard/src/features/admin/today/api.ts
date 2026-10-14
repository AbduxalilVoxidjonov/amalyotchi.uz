import { api } from '@/shared/api';
import type { AttendanceFilter, PresenceFilter, TodayResponse } from './types';

export interface TodayParams {
  status: AttendanceFilter;
  page: number;
  q?: string | undefined;
  /** "Joyida" filtri — `?presence=outside|signalLost` (backend `TodayPresenceFilter`); UI joriy sahifani klientda ham filtrlaydi. */
  presence?: PresenceFilter | undefined;
}

/**
 * Admin · Bugun — `AdminTodayController`:
 *   GET /api/admin/today?status=present|late|absent|excused|pending|suspicious&q=&page=&pageSize= → TodayResponse
 *   (`status` berilmasa — hammasi; noto'g'ri qiymat → 400.)
 *   Har qatorda `presence` (LIVE-JOYLASHUV.md) — jonli joylashuv, shuning uchun 60 s da qayta so'raladi.
 */
export const todayApi = {
  get: (params: TodayParams) =>
    api.get<TodayResponse>('/api/admin/today', {
      query: {
        status: params.status === 'all' ? undefined : params.status,
        q: params.q || undefined,
        page: params.page,
        presence: params.presence && params.presence !== 'all' ? params.presence : undefined,
      },
    }),
};

import { api } from '@/shared/api';
import type { LocationPingsResponse } from './types';

/**
 * Admin · talabaning kunlik joylashuv izi (LIVE-JOYLASHUV.md · Admin API):
 *   GET /api/admin/students/{studentId}/location-pings?date=2026-10-14 → LocationPingsResponse
 *   (data scoping — boshqa admin endpoint'lari kabi; ruxsat bo'lmasa 403/404.)
 */
export const locationApi = {
  pings: (studentId: string, date: string) =>
    api.get<LocationPingsResponse>(
      `/api/admin/students/${encodeURIComponent(studentId)}/location-pings`,
      { query: { date } },
    ),
};

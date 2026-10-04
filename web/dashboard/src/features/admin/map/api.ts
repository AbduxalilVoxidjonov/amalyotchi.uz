import { api } from '@/shared/api';
import type { MapResponse } from './types';

/** Admin · Xarita — `AdminMapController`: GET /api/admin/map?date=2026-10-12 → MapResponse (date berilmasa — bugun) */
export const mapApi = {
  get: (params: { date?: string | undefined } = {}) =>
    api.get<MapResponse>('/api/admin/map', { query: { date: params.date } }),
};

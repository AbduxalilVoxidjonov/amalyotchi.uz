import { api } from '@/shared/api';
import type { MapResponse } from './types';

/** Tyutor · Xarita — `TutorMapController`: GET /api/tutor/map?date=2026-10-12 → MapResponse (date berilmasa — bugun) */
export const mapApi = {
  get: (params: { date?: string | undefined } = {}) =>
    api.get<MapResponse>('/api/tutor/map', { query: { date: params.date } }),
};

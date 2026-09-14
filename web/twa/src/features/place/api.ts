import { api } from '@/shared/api/client';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import type { PracticePlaceDto } from './types';

export const placeApi = {
  get: (signal?: AbortSignal) =>
    api.get<PracticePlaceDto>(STUDENT_ENDPOINTS.place, signal ? { signal } : {}),
};

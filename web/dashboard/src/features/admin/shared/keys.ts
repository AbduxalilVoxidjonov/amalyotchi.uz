import type { ListParams } from './types';

/** TanStack Query kalitlari: `['admin', entity, params]` (shared/api/query-keys.ts konvensiyasi). */
export const adminKeys = {
  all: ['admin'] as const,
  dashboard: () => ['admin', 'dashboard'] as const,
  faculties: (params: ListParams) => ['admin', 'faculties', params] as const,
  /** Barcha `faculties(params)` so'rovlarini invalidate qilish uchun (mutatsiyalardan keyin). */
  facultiesAll: () => ['admin', 'faculties'] as const,
  groups: (params: ListParams) => ['admin', 'groups', params] as const,
  tutors: (params: ListParams) => ['admin', 'tutors', params] as const,
  students: (params: ListParams) => ['admin', 'students', params] as const,
  companies: (params: ListParams) => ['admin', 'companies', params] as const,
  audit: (params: ListParams) => ['admin', 'audit', params] as const,
  settings: () => ['admin', 'settings'] as const,
};

import type { ApplicationTab } from './applications/types';
import type { TodayParams } from './today/api';

/**
 * TanStack Query kalitlari — `['tutor', entity, params?]` (shared/api/query-keys.ts konvensiyasi).
 * Invalidatsiya: `queryClient.invalidateQueries({ queryKey: tutorKeys.applications.all })`.
 */
export const tutorKeys = {
  all: ['tutor'] as const,
  today: (params: TodayParams) => ['tutor', 'today', params] as const,
  applications: {
    all: ['tutor', 'applications'] as const,
    list: (params: { tab: ApplicationTab }) => ['tutor', 'applications', 'list', params] as const,
    detail: (id: string) => ['tutor', 'applications', 'detail', id] as const,
  },
  students: () => ['tutor', 'students'] as const,
  diaries: {
    all: ['tutor', 'diaries'] as const,
    list: () => ['tutor', 'diaries', 'list'] as const,
  },
  calendar: (params: { month: string }) => ['tutor', 'calendar', params] as const,
  map: () => ['tutor', 'map'] as const,
  leaveRequests: {
    all: ['tutor', 'leave-requests'] as const,
    list: () => ['tutor', 'leave-requests', 'list'] as const,
  },
  grading: {
    all: ['tutor', 'grading'] as const,
    list: () => ['tutor', 'grading', 'list'] as const,
  },
};

import type { ApplicationTab } from '../applications/types';
import type { FaceEnrollmentTab } from '../face/types';
import type { TodayParams } from '../today/api';

/**
 * Kundalik nazorat bo'limlari (Bugun, Arizalar, Yuz, Kundaliklar, Kalendar, Xarita, Baholash) va
 * talaba profilidagi davomat/kundalik kalitlari — `['admin', entity, params?]`
 * (shared/api/query-keys.ts konvensiyasi). Ro'yxat/tafsilot kalitlari — `adminKeys` (shared/keys.ts).
 * Invalidatsiya: `queryClient.invalidateQueries({ queryKey: opsKeys.applications.all })`.
 */
export const opsKeys = {
  today: (params: TodayParams) => ['admin', 'today', params] as const,
  applications: {
    all: ['admin', 'applications'] as const,
    list: (params: { tab: ApplicationTab }) => ['admin', 'applications', 'list', params] as const,
    detail: (id: string) => ['admin', 'applications', 'detail', id] as const,
  },
  students: {
    // `periodId` (null — sukut davri) kalitda: davr almashganda kesh aralashmaydi.
    attendance: (
      id: string,
      range: { from: string | null; to: string | null },
      periodId: string | null = null,
    ) => ['admin', 'students', 'attendance', id, periodId, range] as const,
    diaries: (id: string, periodId: string | null = null) =>
      ['admin', 'students', 'diaries', id, periodId] as const,
    locationPings: (id: string, date: string) =>
      ['admin', 'students', 'location-pings', id, date] as const,
  },
  face: {
    all: ['admin', 'face'] as const,
    list: (params: { status: FaceEnrollmentTab }) => ['admin', 'face', 'list', params] as const,
  },
  diaries: {
    all: ['admin', 'diaries'] as const,
    list: () => ['admin', 'diaries', 'list'] as const,
  },
  calendar: (params: { month: string }) => ['admin', 'calendar', params] as const,
  map: () => ['admin', 'map'] as const,
  grading: {
    all: ['admin', 'grading'] as const,
    list: () => ['admin', 'grading', 'list'] as const,
  },
};

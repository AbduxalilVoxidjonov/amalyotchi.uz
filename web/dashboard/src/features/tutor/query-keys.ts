import type { ApplicationTab } from './applications/types';
import type { StudentApiArea } from './students/types';
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
  students: Object.assign(() => ['tutor', 'students'] as const, {
    all: ['tutor', 'students'] as const,
    // `periodId` (null — sukut davri) kalitda: davr almashganda kesh aralashmaydi.
    detail: (id: string, periodId: string | null = null) =>
      ['tutor', 'students', 'detail', id, periodId] as const,
    // `area` — davomat/kundalik tyutor yoki admin endpoint'idan olinganini ajratadi (§ students/api.ts).
    attendance: (
      id: string,
      range: { from: string | null; to: string | null },
      area: StudentApiArea = 'tutor',
      periodId: string | null = null,
    ) => [area, 'students', 'attendance', id, periodId, range] as const,
    diaries: (id: string, area: StudentApiArea = 'tutor', periodId: string | null = null) =>
      [area, 'students', 'diaries', id, periodId] as const,
  }),
  companies: {
    all: ['tutor', 'companies'] as const,
    list: () => ['tutor', 'companies', 'list'] as const,
    detail: (id: string) => ['tutor', 'companies', 'detail', id] as const,
    students: (id: string) => ['tutor', 'companies', 'detail', id, 'students'] as const,
    /** `GET /companies/{id}/checkin-qr` — korxona check-in QR kodi. */
    checkinQr: (id: string) => ['tutor', 'companies', 'detail', id, 'checkin-qr'] as const,
  },
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

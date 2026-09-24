import { api } from '@/shared/api';
import type {
  PracticePeriodCreate,
  PracticePeriodDetail,
  PracticePeriodGroupsUpdate,
  PeriodGroupStudents,
  PracticePeriodListItem,
  PracticePeriodStats,
  PracticePeriodStatus,
  PracticePeriodUpdate,
} from './types';

/**
 * Backend: `AdminPracticePeriodsController` (faqat Admin).
 * GET    /api/admin/practice-periods?status=      → PracticePeriodListItem[] (startDate ↓)
 * GET    /api/admin/practice-periods/{id}         → PracticePeriodDetail · 404
 * POST   /api/admin/practice-periods              → 201 PracticePeriodDetail · 400 · 409 (ustma-ust)
 * PUT    /api/admin/practice-periods/{id}         → 200 PracticePeriodDetail · 400 · 404 · 409
 * PUT    /api/admin/practice-periods/{id}/groups  → 200 PracticePeriodDetail · 400 · 404 · 409
 * POST   /api/admin/practice-periods/{id}/close   → 200 PracticePeriodDetail · 404 · 409
 * DELETE /api/admin/practice-periods/{id}         → 204 · 404 · 409 (davomat yozuvi bor)
 * GET    /api/admin/practice-periods/{id}/stats   → PracticePeriodStats · 404
 * GET    /api/admin/practice-periods/{id}/groups/{groupId}/students
 *                                                  → PeriodGroupStudents · 404 (davr yo'q / guruh davrda emas)
 */
export const PRACTICE_PERIODS_ENDPOINT = '/api/admin/practice-periods';

export const practicePeriodsApi = {
  list: (status: PracticePeriodStatus | null) =>
    api.get<PracticePeriodListItem[]>(PRACTICE_PERIODS_ENDPOINT, {
      query: { status: status ?? undefined },
    }),
  detail: (id: string) => api.get<PracticePeriodDetail>(`${PRACTICE_PERIODS_ENDPOINT}/${id}`),
  create: (body: PracticePeriodCreate) =>
    api.post<PracticePeriodDetail>(PRACTICE_PERIODS_ENDPOINT, body),
  update: (id: string, body: PracticePeriodUpdate) =>
    api.put<PracticePeriodDetail>(`${PRACTICE_PERIODS_ENDPOINT}/${id}`, body),
  setGroups: (id: string, body: PracticePeriodGroupsUpdate) =>
    api.put<PracticePeriodDetail>(`${PRACTICE_PERIODS_ENDPOINT}/${id}/groups`, body),
  close: (id: string) => api.post<PracticePeriodDetail>(`${PRACTICE_PERIODS_ENDPOINT}/${id}/close`),
  remove: (id: string) => api.delete<void>(`${PRACTICE_PERIODS_ENDPOINT}/${id}`),
  stats: (id: string) => api.get<PracticePeriodStats>(`${PRACTICE_PERIODS_ENDPOINT}/${id}/stats`),
  groupStudents: (id: string, groupId: string) =>
    api.get<PeriodGroupStudents>(`${PRACTICE_PERIODS_ENDPOINT}/${id}/groups/${groupId}/students`),
};

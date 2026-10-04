import { api } from '@/shared/api';
import type { GradingRow, GradingUpdateRequest } from './types';

/**
 * Admin · Baholash — `AdminGradingController`:
 *   GET /api/admin/grading             → GradingRow[]
 *   PUT /api/admin/grading/:studentId  {tutorPoints, referencePoints} → GradingRow (jami/baho server hisoblaydi)
 */
export const gradingApi = {
  list: () => api.get<GradingRow[]>('/api/admin/grading'),
  update: (studentId: string, body: GradingUpdateRequest) =>
    api.put<GradingRow>(`/api/admin/grading/${encodeURIComponent(studentId)}`, body),
};

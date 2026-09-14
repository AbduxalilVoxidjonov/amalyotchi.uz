import { api } from '@/shared/api';
import type { GradingRow, GradingUpdateRequest } from './types';

/**
 * Tyutor · Baholash — `TutorGradingController`:
 *   GET /api/tutor/grading             → GradingRow[]
 *   PUT /api/tutor/grading/:studentId  {tutorPoints, referencePoints} → GradingRow (jami/baho server hisoblaydi)
 */
export const gradingApi = {
  list: () => api.get<GradingRow[]>('/api/tutor/grading'),
  update: (studentId: string, body: GradingUpdateRequest) =>
    api.put<GradingRow>(`/api/tutor/grading/${encodeURIComponent(studentId)}`, body),
};

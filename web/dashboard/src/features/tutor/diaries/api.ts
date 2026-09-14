import { api } from '@/shared/api';
import type { DiaryEntry, DiaryReviewRequest, DiaryStatus } from './types';

/**
 * Tyutor · Kundaliklar — `TutorDiariesController`:
 *   GET  /api/tutor/diaries?status=submitted|seen|rewrite|approved → DiaryEntry[] (berilmasa — hammasi)
 *   POST /api/tutor/diaries/:id/review {action, score?, comment?}     → DiaryEntry | 400 | 404 | 409
 */
export const diariesApi = {
  list: (params: { status?: DiaryStatus | undefined } = {}) =>
    api.get<DiaryEntry[]>('/api/tutor/diaries', { query: { status: params.status } }),
  review: (id: string, body: DiaryReviewRequest) =>
    api.post<DiaryEntry>(`/api/tutor/diaries/${encodeURIComponent(id)}/review`, body),
};

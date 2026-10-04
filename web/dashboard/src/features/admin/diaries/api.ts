import { api } from '@/shared/api';
import type { StudentApiArea } from '../students/types';
import type { DiaryEntry, DiaryReviewRequest, DiaryStatus } from './types';

/**
 * Tyutor · Kundaliklar — `TutorDiariesController`:
 *   GET  /api/tutor/diaries?status=submitted|seen|rewrite|approved → DiaryEntry[] (berilmasa — hammasi)
 *   POST /api/tutor/diaries/:id/review {action, score?, comment?}     → DiaryEntry | 400 | 404 | 409
 *
 * Baholash admin panelida ham bor (`AdminDiariesController` — `POST /api/admin/diaries/:id/review`,
 * ayni buyruq va qoidalar), shuning uchun yo'l `area` bilan tanlanadi.
 */
export const diariesApi = {
  list: (params: { status?: DiaryStatus | undefined } = {}) =>
    api.get<DiaryEntry[]>('/api/tutor/diaries', { query: { status: params.status } }),
  review: (id: string, body: DiaryReviewRequest, area: StudentApiArea = 'tutor') =>
    api.post<DiaryEntry>(`/api/${area}/diaries/${encodeURIComponent(id)}/review`, body),
};

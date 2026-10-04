import { api } from '@/shared/api';
import type { DiaryEntry, DiaryReviewRequest, DiaryStatus } from './types';

/**
 * Admin · Kundaliklar (v3.28 — sobiq tyutor bo'limidan):
 *   GET  /api/admin/diaries?status=submitted|seen|rewrite|approved → DiaryEntry[] (berilmasa — hammasi)
 *   POST /api/admin/diaries/:id/review {action, score?, comment?}   → DiaryEntry | 400 | 404 | 409
 * Baholash ham shu sahifadagi kartada, ham talaba profilidagi kun oynasida ishlatiladi.
 */
export const diariesApi = {
  list: (params: { status?: DiaryStatus | undefined } = {}) =>
    api.get<DiaryEntry[]>('/api/admin/diaries', { query: { status: params.status } }),
  review: (id: string, body: DiaryReviewRequest) =>
    api.post<DiaryEntry>(`/api/admin/diaries/${encodeURIComponent(id)}/review`, body),
};

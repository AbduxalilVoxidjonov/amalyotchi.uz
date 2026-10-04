import { api } from '@/shared/api';
import type {
  ApplicationDecisionRequest,
  ApplicationDecisionResponse,
  ApplicationDetail,
  ApplicationListResponse,
  ApplicationTab,
} from './types';

/**
 * Tyutor · Arizalar — `TutorApplicationsController`:
 *   GET  /api/tutor/applications?status=submitted|revisionNeeded|approved|rejected → ApplicationListResponse
 *   GET  /api/tutor/applications/:id                                               → ApplicationDetail | 404
 *   POST /api/tutor/applications/:id/decision {decision, radiusM?, checklist?, comment?}
 *        → ApplicationDecisionResponse | 400 errors | 404 | 409 (allaqachon hal qilingan)
 */
export const applicationsApi = {
  list: (params: { tab: ApplicationTab }) =>
    api.get<ApplicationListResponse>('/api/tutor/applications', {
      query: { status: params.tab },
    }),
  detail: (id: string) =>
    api.get<ApplicationDetail>(`/api/tutor/applications/${encodeURIComponent(id)}`),
  decide: (id: string, body: ApplicationDecisionRequest) =>
    api.post<ApplicationDecisionResponse>(
      `/api/tutor/applications/${encodeURIComponent(id)}/decision`,
      body,
    ),
};

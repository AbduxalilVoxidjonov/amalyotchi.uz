import { api } from '@/shared/api';
import type {
  ApplicationDecisionRequest,
  ApplicationDecisionResponse,
  ApplicationDetail,
  ApplicationListResponse,
  ApplicationTab,
} from './types';

/**
 * Admin · Arizalar — `AdminApplicationsController`:
 *   GET  /api/admin/applications?status=submitted|revisionNeeded|approved|rejected → ApplicationListResponse
 *   GET  /api/admin/applications/:id                                               → ApplicationDetail | 404
 *   POST /api/admin/applications/:id/decision {decision, radiusM?, checklist?, comment?}
 *        → ApplicationDecisionResponse | 400 errors | 404 | 409 (allaqachon hal qilingan)
 */
export const applicationsApi = {
  list: (params: { tab: ApplicationTab }) =>
    api.get<ApplicationListResponse>('/api/admin/applications', {
      query: { status: params.tab },
    }),
  detail: (id: string) =>
    api.get<ApplicationDetail>(`/api/admin/applications/${encodeURIComponent(id)}`),
  decide: (id: string, body: ApplicationDecisionRequest) =>
    api.post<ApplicationDecisionResponse>(
      `/api/admin/applications/${encodeURIComponent(id)}/decision`,
      body,
    ),
};

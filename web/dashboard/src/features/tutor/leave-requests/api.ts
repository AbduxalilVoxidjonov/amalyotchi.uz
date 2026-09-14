import { api } from '@/shared/api';
import type { LeaveDecisionRequest, LeaveRequest, LeaveRequestStatus } from './types';

/**
 * Tyutor · Ruxsat so'rovlari — `TutorLeaveRequestsController`:
 *   GET  /api/tutor/leave-requests?status=pending|approved|rejected   → LeaveRequest[] (berilmasa — hammasi)
 *   POST /api/tutor/leave-requests/:id/decision {decision, comment?}  → LeaveRequest | 400 | 404 | 409
 */
export const leaveRequestsApi = {
  list: (params: { status?: LeaveRequestStatus | undefined } = {}) =>
    api.get<LeaveRequest[]>('/api/tutor/leave-requests', { query: { status: params.status } }),
  decide: (id: string, body: LeaveDecisionRequest) =>
    api.post<LeaveRequest>(`/api/tutor/leave-requests/${encodeURIComponent(id)}/decision`, body),
};

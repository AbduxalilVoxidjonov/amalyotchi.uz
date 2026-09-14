import type { StatusKind } from '@/shared/ui';

export type LeaveRequestStatus = 'pending' | 'approved' | 'rejected';

/** Hujjat: fayl yuklangan bo'lsa `url`, aks holda faqat nom (`url: null`). */
export interface LeaveDocument {
  name: string;
  url: string | null;
}

/** GET /api/tutor/leave-requests?status= → TutorLeaveRequest[] (kutilayotganlar birinchi) */
export interface LeaveRequest {
  id: string;
  studentId: string;
  studentName: string;
  group: string;
  /** DateOnly */
  dateFrom: string;
  /** DateOnly (bir kunlik bo'lsa dateFrom bilan teng) */
  dateTo: string;
  reason: string;
  document: LeaveDocument | null;
  status: LeaveRequestStatus;
  /** Tyutor qaror izohi. */
  comment: string | null;
  createdAt: string;
  decidedAt: string | null;
}

export type LeaveDecision = 'approve' | 'reject';

/** POST /api/tutor/leave-requests/:id/decision → LeaveRequest | 409 (hal qilingan) */
export interface LeaveDecisionRequest {
  decision: LeaveDecision;
  comment?: string;
}

export const LEAVE_STATUS_LABEL: Record<LeaveRequestStatus, { label: string; kind: StatusKind }> = {
  pending: { label: 'Kutilmoqda', kind: 'info' },
  approved: { label: 'Tasdiqlangan', kind: 'ok' },
  rejected: { label: 'Rad etilgan', kind: 'bad' },
};

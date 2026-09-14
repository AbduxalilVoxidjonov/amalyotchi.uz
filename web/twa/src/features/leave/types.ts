import type { StatusKind } from '@amaliyotchi/shared/ui';

/** Domain `LeaveRequestStatus`. */
export type LeaveRequestStatus = 'pending' | 'approved' | 'rejected';

export interface LeaveDocumentDto {
  name: string;
  /** null — fayl hali yuklanmagan (faqat nom). */
  url: string | null;
}

/** GET /api/student/leave-requests (`LeaveRequestDto`). */
export interface LeaveRequestDto {
  id: string;
  /** DateOnly */
  dateFrom: string;
  dateTo: string;
  reason: string;
  status: LeaveRequestStatus;
  /** Tyutor qarori izohi. */
  comment: string | null;
  document: LeaveDocumentDto | null;
  /** ISO datetime */
  createdAt: string;
}

/**
 * POST /api/student/leave-requests — JSON (`CreateLeaveRequestCommand`). Sanalar davr ichida;
 * kesishuvchi kutilayotgan/tasdiqlangan so'rov → 409. `attachmentFileId` — talaba yuklagan fayl
 * (yuklash endpoint'i hali yo'q ❓ — hozircha faqat `attachmentName`).
 */
export interface LeaveRequestCreate {
  dateFrom: string;
  dateTo: string;
  reason: string;
  attachmentName?: string;
  attachmentFileId?: string;
}

export const LEAVE_STATUS: Record<LeaveRequestStatus, { label: string; kind: StatusKind }> = {
  pending: { label: 'Kutilmoqda', kind: 'info' },
  approved: { label: 'Tasdiqlangan', kind: 'ok' },
  rejected: { label: 'Rad etilgan', kind: 'bad' },
};

/** Backend `LeaveRequest.MinReasonLength`. */
export const LEAVE_REASON_MIN = 10;

import type { StatusKind } from '@amaliyotchi/shared/ui';

/** Domain `ApplicationStatus` (Practice) — JSON'da camelCase string. */
export type ApplicationStatus =
  'draft' | 'submitted' | 'revisionNeeded' | 'approved' | 'rejected' | 'completed';

/** Shartnoma fayli (`PracticeContractDto`). */
export interface PracticeContractDto {
  fileId: string;
  fileName: string;
  pages: number | null;
  sizeBytes: number;
  /** ISO datetime */
  uploadedAt: string;
  /** null — hali tasdiqlanmagan */
  approvedAt: string | null;
  approvedBy: string | null;
  /** Shablon havolasi (WebApp.openLink) | null */
  templateUrl: string | null;
}

/** GET /api/student/place (`PracticePlaceDto`). Ariza yo'q → 404. */
export interface PracticePlaceDto {
  status: ApplicationStatus;
  /** Tyutor izohi (qaytarilgan/rad etilgan arizada). */
  comment: string | null;
  company: string;
  /** STIR "304512889" — UI 3-3-3 qilib ko'rsatadi. */
  tin: string;
  activity: string;
  address: string;
  supervisorName: string;
  supervisorPhone: string;
  mentorName: string | null;
  mentorPhone: string | null;
  radiusM: number;
  lat: number;
  lng: number;
  /** DateOnly — amaliyot davri. */
  periodFrom: string;
  periodTo: string;
  contract: PracticeContractDto | null;
}

/** Ariza holati → o'zbekcha yorliq + Badge rangi. */
export const APPLICATION_STATUS: Record<ApplicationStatus, { label: string; kind: StatusKind }> = {
  draft: { label: 'Qoralama', kind: 'neu' },
  submitted: { label: 'Tekshiruvda', kind: 'info' },
  revisionNeeded: { label: 'Qayta topshirish', kind: 'late' },
  approved: { label: 'Tasdiqlangan', kind: 'ok' },
  rejected: { label: 'Rad etilgan', kind: 'bad' },
  completed: { label: 'Yakunlangan', kind: 'neu' },
};

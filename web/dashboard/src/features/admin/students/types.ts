import type { TutorStudentDetail } from '@/features/tutor/students/types';

/** Kontrakt v2 `Student` (backend `StudentRow`). Holat: Faol (ok) · Qizil bayroq (bad) · Ulanmagan (neu). */
export type StudentStatus = 'active' | 'flagged' | 'unlinked';

export interface Student {
  /** Talabaning `User.Id`. */
  id: string;
  fullName: string;
  hemisId: string;
  groupId: string;
  /** "412-22" */
  group: string;
  course: number;
  faculty: string;
  /** Tasdiqlangan arizadagi korxona, yo'q bo'lsa null ("—"). */
  company: string | null;
  attendancePct: number;
  suspiciousDays: number;
  telegramLinked: boolean;
  status: StudentStatus;
}

export const STUDENT_STATUS_LABEL: Record<
  StudentStatus,
  { label: string; kind: 'ok' | 'bad' | 'neu' }
> = {
  active: { label: 'Faol', kind: 'ok' },
  flagged: { label: 'Qizil bayroq', kind: 'bad' },
  unlinked: { label: 'Ulanmagan', kind: 'neu' },
};

/* ────────────────────────────────────────────────────────────────────────────
 * Talaba profili (`/admin/students/:studentId`) — `GET /api/admin/students/{id}`.
 * Bloklar tyutor profilidagi bilan bir xil (ayni backend handler), shuning uchun
 * turlar `features/tutor/students/types` dan olinadi; bu yerda faqat admin qo'shimchalari.
 * ──────────────────────────────────────────────────────────────────────────── */

/** Talaba guruhiga biriktirilgan tyutor (bir nechtasi bo'lsa — FISH bo'yicha birinchisi). */
export interface AdminStudentTutor {
  id: string;
  fullName: string;
  phone: string | null;
}

/** `GET /api/admin/students/{id}` → tyutor profili maydonlari + adminga xoslari. */
export interface AdminStudentDetail extends TutorStudentDetail {
  groupId: string;
  /** Kafedra nomi (tyutor profilida yo'q). */
  department: string;
  /** Ro'yxatdagi holat bilan bir xil qoida (faol · qizil bayroq · ulanmagan). */
  adminStatus: StudentStatus;
  telegramLinked: boolean;
  tutor: AdminStudentTutor | null;
}

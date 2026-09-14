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

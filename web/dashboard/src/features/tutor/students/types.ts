import type { StatusKind } from '@/shared/ui';

/** Backend `StudentState`: davomat < 70% → redFlag; shubhali kunlar bor → suspicious; aks holda active. */
export type StudentState = 'active' | 'redFlag' | 'suspicious';

/** GET /api/tutor/students → TutorStudent[] */
export interface TutorStudent {
  id: string;
  name: string;
  hemisId: string;
  group: string;
  /** Tasdiqlangan ariza bo'lmasa null → "—". */
  company: string | null;
  /** 0–100 (kasrli bo'lishi mumkin: 84.6). */
  attendancePct: number;
  attendedDays: number;
  totalDays: number;
  diaryCount: number;
  /** O'rtacha ball (4.2); kundalik yo'q bo'lsa 0. */
  diaryAvg: number;
  state: StudentState;
  /** Shubhali kunlar soni ("3 shubhali"). */
  suspiciousCount: number;
}

export const STUDENT_STATE_LABEL: Record<StudentState, { label: string; kind: StatusKind }> = {
  active: { label: 'Faol', kind: 'ok' },
  redFlag: { label: 'Qizil bayroq', kind: 'bad' },
  suspicious: { label: 'Shubhali', kind: 'late' },
};

export function studentStateLabel(s: TutorStudent): { label: string; kind: StatusKind } {
  const base = STUDENT_STATE_LABEL[s.state];
  return s.state === 'suspicious' ? { ...base, label: `${s.suspiciousCount} shubhali` } : base;
}

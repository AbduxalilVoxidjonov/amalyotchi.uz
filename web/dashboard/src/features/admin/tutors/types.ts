/** Kontrakt v2 `Tutor` (backend `TutorRow`). Holat: Faol (ok) · Kechikmoqda (bad) — ariza 3 kundan ortiq kutmoqda. */
export type TutorStatus = 'active' | 'late';

export interface Tutor {
  id: string;
  fullName: string;
  /** E.164 "+998901234567" (UI: "+998 90 123-45-67"); null bo'lishi mumkin. */
  phone: string | null;
  facultyId: string | null;
  facultyCode: string | null;
  facultyName: string | null;
  /** Guruh kodlari. */
  groups: string[];
  students: number;
  /** Kutayotgan arizalar. */
  pending: number;
  oldestPendingAt: string | null;
  /** Qaror tezligi (soat), qaror bo'lmasa null. */
  avgDecisionHours: number | null;
  /** Oxirgi kirish yoki oxirgi amal (ISO), null — hech qachon. */
  lastActiveAt: string | null;
  isActive: boolean;
  status: TutorStatus;
}

export const TUTOR_STATUS_LABEL: Record<TutorStatus, { label: string; kind: 'ok' | 'bad' }> = {
  active: { label: 'Faol', kind: 'ok' },
  late: { label: 'Kechikmoqda', kind: 'bad' },
};

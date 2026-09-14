/** Kontrakt v2 `Faculty` (backend `FacultyRow`). Holat: Faol (ok) · E'tibor (late) — bugungi davomat past. */
export type FacultyStatus = 'active' | 'attention';

export interface Faculty {
  id: string;
  name: string;
  /** "AT" */
  code: string;
  /** Yo'nalishlar soni. */
  directions: number;
  groups: number;
  students: number;
  tutors: number;
  /** Bugungi davomat (kutilganlardan kelgan/kech kelgan), kutilgan bo'lmasa 0. */
  attendancePct: number;
  status: FacultyStatus;
}

export const FACULTY_STATUS_LABEL: Record<FacultyStatus, { label: string; kind: 'ok' | 'late' }> = {
  active: { label: 'Faol', kind: 'ok' },
  attention: { label: "E'tibor", kind: 'late' },
};

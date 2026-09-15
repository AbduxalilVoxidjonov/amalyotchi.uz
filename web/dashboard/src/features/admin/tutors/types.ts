import type { ListParams } from '../shared/types';

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

/** `GET /api/admin/tutors?q&facultyId&page&pageSize` — `facultyId` filtri ixtiyoriy. */
export interface TutorListParams extends ListParams {
  facultyId?: string;
}

/** `TutorDetail.groups[]` — faol o'quv yilidagi biriktirma. */
export interface TutorGroup {
  assignmentId: string;
  groupId: string;
  groupName: string;
  course: number;
  directionName: string;
  students: number;
  academicYearName: string;
  isActive: boolean;
}

/** `GET/POST/PUT /api/admin/tutors/{id}` javobi. */
export interface TutorDetail {
  id: string;
  fullName: string;
  hemisId: string;
  phone: string | null;
  facultyId: string;
  facultyCode: string;
  facultyName: string;
  isActive: boolean;
  lastLoginAt: string | null;
  createdAt: string;
  groups: TutorGroup[];
}

/** `POST /api/admin/tutors` body'si. */
export interface TutorCreateInput {
  fullName: string;
  hemisId: string;
  phone?: string | null;
  password: string;
  facultyId: string;
}

/** `PUT /api/admin/tutors/{id}` body'si (parol alohida endpoint). */
export interface TutorUpdateInput {
  fullName: string;
  phone?: string | null;
  facultyId: string;
}

/** `GET /api/admin/tutors/{id}/available-groups` qatori — tyutor fakultetidagi barcha faol guruhlar. */
export interface AvailableGroup {
  id: string;
  name: string;
  course: number;
  directionName: string;
  departmentName: string;
  students: number;
  /** Boshqa tyutorga biriktirilgan bo'lsa — uning id/FISH'i; bo'sh — null. */
  tutorId: string | null;
  tutorName: string | null;
}

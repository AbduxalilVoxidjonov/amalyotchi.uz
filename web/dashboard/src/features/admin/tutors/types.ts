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

/** Ko'lam darajasi: fakultet → kafedra → yo'nalish → guruh (ota tanlansa — ichidagi hamma narsa). */
export type TutorScopeLevel = 'faculty' | 'department' | 'direction' | 'group';

export const SCOPE_LEVEL_LABEL: Record<TutorScopeLevel, string> = {
  faculty: 'Fakultet',
  department: 'Kafedra',
  direction: "Yo'nalish",
  group: 'Guruh',
};

/** `TutorDetail.scopes[]` — tyutorga aniq biriktirilgan tugun (ko'lam). */
export interface TutorScope {
  id: string;
  level: TutorScopeLevel;
  facultyId: string;
  departmentId: string | null;
  directionId: string | null;
  groupId: string | null;
  /** Tanlangan tugun nomi. */
  name: string;
  /** Ota tugunlar: "Axborot texnologiyalari › Umumiy kafedra" (fakultet darajasida ""). */
  path: string;
  /** Qamrab olingan faol guruhlar soni. */
  groups: number;
  students: number;
}

/** `PUT /api/admin/tutors/{id}/scopes` body elementi. */
export interface TutorScopeInput {
  level: TutorScopeLevel;
  id: string;
}

/** `GET/POST/PUT /api/admin/tutors/{id}` javobi. `groups` — ko'lamlardan yoyilgan samarali guruhlar (backend hisoblaydi). */
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
  scopes: TutorScope[];
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

/** AYNAN shu tugunda ko'lami bor tyutor (joriy tyutorning o'zi ham bo'lishi mumkin); yo'q — null. */
export interface ScopeNodeOwner {
  tutorId: string | null;
  tutorName: string | null;
}

export interface ScopeTreeGroup extends ScopeNodeOwner {
  id: string;
  name: string;
  course: number;
  students: number;
}

export interface ScopeTreeDirection extends ScopeNodeOwner {
  id: string;
  name: string;
  groups: ScopeTreeGroup[];
}

export interface ScopeTreeDepartment extends ScopeNodeOwner {
  id: string;
  name: string;
  directions: ScopeTreeDirection[];
}

/** `GET /api/admin/tutors/{id}/scope-tree` — tyutor fakulteti daraxti (faol tugunlar). */
export interface TutorScopeTree extends ScopeNodeOwner {
  id: string;
  name: string;
  code: string;
  departments: ScopeTreeDepartment[];
}

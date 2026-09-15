import type { ListParams } from './types';

/** TanStack Query kalitlari: `['admin', entity, params]` (shared/api/query-keys.ts konvensiyasi). */
export const adminKeys = {
  all: ['admin'] as const,
  dashboard: () => ['admin', 'dashboard'] as const,
  faculties: (params: ListParams) => ['admin', 'faculties', params] as const,
  /** Barcha `faculties(params)` so'rovlarini invalidate qilish uchun (mutatsiyalardan keyin). */
  facultiesAll: () => ['admin', 'faculties'] as const,
  /** `GET /faculties/{id}` — ierarxiya breadcrumb'i. */
  faculty: (id: string) => ['admin', 'faculty', id] as const,

  /** Ierarxiya: Fakultet → Kafedra → Yo'nalish → Guruh. Ro'yxatlar ota id'siga bog'liq. */
  departments: (facultyId: string, params: ListParams) =>
    ['admin', 'departments', facultyId, params] as const,
  departmentsAll: (facultyId: string) => ['admin', 'departments', facultyId] as const,
  department: (id: string) => ['admin', 'department', id] as const,

  directions: (departmentId: string, params: ListParams) =>
    ['admin', 'directions', departmentId, params] as const,
  directionsAll: (departmentId: string) => ['admin', 'directions', departmentId] as const,
  direction: (id: string) => ['admin', 'direction', id] as const,

  groups: (directionId: string, params: ListParams) =>
    ['admin', 'groups', directionId, params] as const,
  groupsAll: (directionId: string) => ['admin', 'groups', directionId] as const,
  group: (id: string) => ['admin', 'group', id] as const,

  tutors: (params: ListParams) => ['admin', 'tutors', params] as const,
  /** Barcha `tutors(params)` so'rovlarini invalidate qilish uchun. */
  tutorsAll: () => ['admin', 'tutors'] as const,
  /** `GET /tutors/{id}` — detail sahifasi; `available-groups` ham shu prefiks ostida. */
  tutor: (id: string) => ['admin', 'tutor', id] as const,
  tutorAvailableGroups: (id: string) => ['admin', 'tutor', id, 'available-groups'] as const,
  students: (params: ListParams) => ['admin', 'students', params] as const,
  companies: (params: ListParams) => ['admin', 'companies', params] as const,
  audit: (params: ListParams) => ['admin', 'audit', params] as const,
  settings: () => ['admin', 'settings'] as const,
};

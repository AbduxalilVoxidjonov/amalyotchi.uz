import { api } from '@/shared/api';
import { toQuery, type Paged } from '../shared/types';
import type {
  AvailableGroup,
  Tutor,
  TutorCreateInput,
  TutorDetail,
  TutorListParams,
  TutorUpdateInput,
} from './types';

/** Backend: `AdminTutorsController`. Barchasi admin, JSON camelCase, xatolar ProblemDetails. */
export const TUTORS_ENDPOINT = '/api/admin/tutors';

export const tutorsApi = {
  /** GET ?q&facultyId&page&pageSize → Paged<Tutor> (`q`: ism, telefon, fakultet). */
  list: (params: TutorListParams) =>
    api.get<Paged<Tutor>>(TUTORS_ENDPOINT, {
      query: { ...toQuery(params), facultyId: params.facultyId || undefined },
    }),
  /** GET /{id} → TutorDetail. 404. */
  get: (id: string) => api.get<TutorDetail>(`${TUTORS_ENDPOINT}/${id}`),
  /** POST → 201 TutorDetail. 400 validatsiya; 409 "Bu HEMIS ID bilan foydalanuvchi mavjud." */
  create: (body: TutorCreateInput) => api.post<TutorDetail>(TUTORS_ENDPOINT, body),
  /** PUT /{id} → 200 TutorDetail. 409 fakultet o'zgartirilganda guruhlar biriktirilgan bo'lsa. */
  update: (id: string, body: TutorUpdateInput) =>
    api.put<TutorDetail>(`${TUTORS_ENDPOINT}/${id}`, body),
  /** PATCH /{id}/status → 204. */
  setStatus: (id: string, isActive: boolean) =>
    api.patch<void>(`${TUTORS_ENDPOINT}/${id}/status`, { isActive }),
  /** POST /{id}/password `{ password }` (min 8) → 204. */
  resetPassword: (id: string, password: string) =>
    api.post<void>(`${TUTORS_ENDPOINT}/${id}/password`, { password }),
  /** PUT /{id}/groups `{ groupIds }` → 200 TutorDetail (to'plamni almashtiradi). 409 band guruh / faol o'quv yili yo'q. */
  setGroups: (id: string, groupIds: string[]) =>
    api.put<TutorDetail>(`${TUTORS_ENDPOINT}/${id}/groups`, { groupIds }),
  /** GET /{id}/available-groups → AvailableGroup[] (tyutor fakultetidagi faol guruhlar). */
  availableGroups: (id: string) =>
    api.get<AvailableGroup[]>(`${TUTORS_ENDPOINT}/${id}/available-groups`),
};

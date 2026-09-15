import { api } from '@/shared/api';
import { toQuery, type Paged } from '../shared/types';
import type {
  Tutor,
  TutorCreateInput,
  TutorDetail,
  TutorListParams,
  TutorScopeInput,
  TutorScopeTree,
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
  /**
   * POST → 201 TutorDetail. 400 validatsiya (bo'sh `facultyIds` ham); 404 fakultet topilmasa;
   * 409 "Bu HEMIS ID bilan foydalanuvchi mavjud." / "Fakultet faol emas: <nom>".
   */
  create: (body: TutorCreateInput) => api.post<TutorDetail>(TUTORS_ENDPOINT, body),
  /** PUT /{id} → 200 TutorDetail. 409 ko'lami bor fakultet `facultyIds` dan olib tashlansa. */
  update: (id: string, body: TutorUpdateInput) =>
    api.put<TutorDetail>(`${TUTORS_ENDPOINT}/${id}`, body),
  /** PATCH /{id}/status → 204. */
  setStatus: (id: string, isActive: boolean) =>
    api.patch<void>(`${TUTORS_ENDPOINT}/${id}/status`, { isActive }),
  /** POST /{id}/password `{ password }` (min 8) → 204. */
  resetPassword: (id: string, password: string) =>
    api.post<void>(`${TUTORS_ENDPOINT}/${id}/password`, { password }),
  /**
   * PUT /{id}/scopes `{ scopes: [{ level, id }] }` → 200 TutorDetail (to'plamni almashtiradi).
   * 409 "<nom> (<daraja>) <FISH> tyutoriga biriktirilgan." / "Faol o'quv yili yo'q."; 400 — detail.
   */
  setScopes: (id: string, scopes: TutorScopeInput[]) =>
    api.put<TutorDetail>(`${TUTORS_ENDPOINT}/${id}/scopes`, { scopes }),
  /** GET /{id}/scope-tree → TutorScopeTree[] (har fakultet uchun: kafedra → yo'nalish → guruh, egalari bilan). */
  scopeTree: (id: string) => api.get<TutorScopeTree[]>(`${TUTORS_ENDPOINT}/${id}/scope-tree`),
};

import { api } from '@/shared/api';
import { toQuery, type ListParams, type Paged } from '../../shared/types';
import type { GroupDto, GroupInput, GroupRow } from './types';

/**
 * Backend: ierarxiya kontrakti (hierarchy-contract.md) — `Groups`, yo'nalish ostida.
 * Global ro'yxat (`GET /api/admin/groups`) backendda qoladi, lekin frontendda ishlatilmaydi
 * (API-CONTRACT.md'da izohlangan).
 */
export const groupsApi = {
  /** GET /api/admin/directions/{directionId}/groups?q&page → Paged<GroupRow>. */
  list: (directionId: string, params: ListParams) =>
    api.get<Paged<GroupRow>>(`/api/admin/directions/${directionId}/groups`, {
      query: toQuery(params),
    }),
  /** POST /api/admin/directions/{directionId}/groups — 201 GroupDto. 400/404/409 (nom takrori/faol o'quv yili yo'q). */
  create: (directionId: string, body: GroupInput) =>
    api.post<GroupDto>(`/api/admin/directions/${directionId}/groups`, body),
  /** GET /api/admin/groups/{id}. 404. */
  get: (id: string) => api.get<GroupDto>(`/api/admin/groups/${id}`),
  /** PUT /api/admin/groups/{id} — 200 GroupDto. 400/404/409. */
  update: (id: string, body: GroupInput) => api.put<GroupDto>(`/api/admin/groups/${id}`, body),
  /** PATCH /api/admin/groups/{id}/status — 200 GroupDto. 404. */
  setStatus: (id: string, isActive: boolean) =>
    api.patch<GroupDto>(`/api/admin/groups/${id}/status`, { isActive }),
  /** DELETE /api/admin/groups/{id} — 204. 404; 409 talaba/tyutor biriktirilgan. */
  remove: (id: string) => api.delete<void>(`/api/admin/groups/${id}`),
};

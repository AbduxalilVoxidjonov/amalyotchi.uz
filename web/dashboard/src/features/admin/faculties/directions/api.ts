import { api } from '@/shared/api';
import { toQuery, type ListParams, type Paged } from '../../shared/types';
import type { DirectionDto, DirectionInput, DirectionRow } from './types';

/** Backend: ierarxiya kontrakti (hierarchy-contract.md) — `Directions` (Yo'nalish). */
export const directionsApi = {
  /** GET /api/admin/departments/{departmentId}/directions?q&page → Paged<DirectionRow>. */
  list: (departmentId: string, params: ListParams) =>
    api.get<Paged<DirectionRow>>(`/api/admin/departments/${departmentId}/directions`, {
      query: toQuery(params),
    }),
  /** POST /api/admin/departments/{departmentId}/directions — 201 DirectionDto. 400/404/409. */
  create: (departmentId: string, body: DirectionInput) =>
    api.post<DirectionDto>(`/api/admin/departments/${departmentId}/directions`, body),
  /** GET /api/admin/directions/{id} — breadcrumb uchun. 404. */
  get: (id: string) => api.get<DirectionDto>(`/api/admin/directions/${id}`),
  /** PUT /api/admin/directions/{id} — 200 DirectionDto. 400/404/409. */
  update: (id: string, body: DirectionInput) =>
    api.put<DirectionDto>(`/api/admin/directions/${id}`, body),
  /** PATCH /api/admin/directions/{id}/status — 200 DirectionDto. 404. */
  setStatus: (id: string, isActive: boolean) =>
    api.patch<DirectionDto>(`/api/admin/directions/${id}/status`, { isActive }),
  /** DELETE /api/admin/directions/{id} — 204. 404; 409 guruhlari bor. */
  remove: (id: string) => api.delete<void>(`/api/admin/directions/${id}`),
};

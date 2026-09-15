import { api } from '@/shared/api';
import { toQuery, type ListParams, type Paged } from '../../shared/types';
import type { DepartmentDto, DepartmentInput, DepartmentRow } from './types';

/** Backend: ierarxiya kontrakti (hierarchy-contract.md) — `Departments` (Kafedra). */
export const departmentsApi = {
  /** GET /api/admin/faculties/{facultyId}/departments?q&page → Paged<DepartmentRow>. */
  list: (facultyId: string, params: ListParams) =>
    api.get<Paged<DepartmentRow>>(`/api/admin/faculties/${facultyId}/departments`, {
      query: toQuery(params),
    }),
  /** POST /api/admin/faculties/{facultyId}/departments — 201 DepartmentDto. 400/404/409 (kod takrori). */
  create: (facultyId: string, body: DepartmentInput) =>
    api.post<DepartmentDto>(`/api/admin/faculties/${facultyId}/departments`, body),
  /** GET /api/admin/departments/{id} — breadcrumb uchun. 404. */
  get: (id: string) => api.get<DepartmentDto>(`/api/admin/departments/${id}`),
  /** PUT /api/admin/departments/{id} — 200 DepartmentDto. 400/404/409. */
  update: (id: string, body: DepartmentInput) =>
    api.put<DepartmentDto>(`/api/admin/departments/${id}`, body),
  /** PATCH /api/admin/departments/{id}/status — 200 DepartmentDto. 404. */
  setStatus: (id: string, isActive: boolean) =>
    api.patch<DepartmentDto>(`/api/admin/departments/${id}/status`, { isActive }),
  /** DELETE /api/admin/departments/{id} — 204. 404; 409 yo'nalishlari bor. */
  remove: (id: string) => api.delete<void>(`/api/admin/departments/${id}`),
};

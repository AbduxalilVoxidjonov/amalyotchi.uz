import { api } from '@/shared/api';
import { toQuery, type ListParams, type Paged } from '../shared/types';
import type { Faculty, FacultyDto, FacultyInput } from './types';

/** Backend: `AdminFacultiesController`. GET /api/admin/faculties?q=&page=&pageSize= → Paged<Faculty> (`q`: nom yoki kod). */
export const FACULTIES_ENDPOINT = '/api/admin/faculties';

export const facultiesApi = {
  list: (params: ListParams) =>
    api.get<Paged<Faculty>>(FACULTIES_ENDPOINT, { query: toQuery(params) }),
  /** POST — 201 FacultyDto. 400 errors.Name/errors.Code; 409 kod takrori. */
  create: (body: FacultyInput) => api.post<FacultyDto>(FACULTIES_ENDPOINT, body),
  /** PUT /{id} — 200 FacultyDto. 400 errors.Name/errors.Code; 404; 409 kod takrori. */
  update: (id: string, body: FacultyInput) =>
    api.put<FacultyDto>(`${FACULTIES_ENDPOINT}/${id}`, body),
  /** PATCH /{id}/status — 200 FacultyDto. 404. */
  setStatus: (id: string, isActive: boolean) =>
    api.patch<FacultyDto>(`${FACULTIES_ENDPOINT}/${id}/status`, { isActive }),
  /** DELETE /{id} — 204. 404; 409 bog'liq guruh/tyutor/talaba bor. */
  remove: (id: string) => api.delete<void>(`${FACULTIES_ENDPOINT}/${id}`),
};

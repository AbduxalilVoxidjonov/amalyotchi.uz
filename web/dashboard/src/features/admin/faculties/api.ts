import { api } from '@/shared/api';
import { toQuery, type ListParams, type Paged } from '../shared/types';
import type { Faculty } from './types';

/** Backend: `AdminFacultiesController`. GET /api/admin/faculties?q=&page=&pageSize= → Paged<Faculty> (`q`: nom yoki kod). */
export const FACULTIES_ENDPOINT = '/api/admin/faculties';

export const facultiesApi = {
  list: (params: ListParams) =>
    api.get<Paged<Faculty>>(FACULTIES_ENDPOINT, { query: toQuery(params) }),
};

import { api } from '@/shared/api';
import { toQuery, type ListParams, type Paged } from '../shared/types';
import type { Tutor } from './types';

/** Backend: `AdminTutorsController`. GET /api/admin/tutors?q=&page=&pageSize= → Paged<Tutor> (`q`: ism, telefon, fakultet). */
export const TUTORS_ENDPOINT = '/api/admin/tutors';

export const tutorsApi = {
  list: (params: ListParams) => api.get<Paged<Tutor>>(TUTORS_ENDPOINT, { query: toQuery(params) }),
};

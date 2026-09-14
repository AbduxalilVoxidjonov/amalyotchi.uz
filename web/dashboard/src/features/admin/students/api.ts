import { api } from '@/shared/api';
import { toQuery, type ListParams, type Paged } from '../shared/types';
import type { Student } from './types';

/** Backend: `AdminStudentsController`. GET /api/admin/students?q=&page=&pageSize= → Paged<Student> (`q`: FISH, HEMIS ID, telefon, guruh). */
export const STUDENTS_ENDPOINT = '/api/admin/students';

export const studentsApi = {
  list: (params: ListParams) =>
    api.get<Paged<Student>>(STUDENTS_ENDPOINT, { query: toQuery(params) }),
};

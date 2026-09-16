import { api } from '@/shared/api';
import { toQuery, type ListParams, type Paged } from '../shared/types';
import type { AdminStudentDetail, Student } from './types';

/**
 * Backend: `AdminStudentsController`.
 * GET /api/admin/students?q=&page=&pageSize= → Paged<Student> (`q`: FISH, HEMIS ID, telefon, guruh)
 * GET /api/admin/students/{id}               → AdminStudentDetail · 404
 * Davomat va kundaliklar (`{id}/attendance`, `{id}/diaries`) — tyutor profilidagi bilan bir xil
 * shakl; ular `features/tutor/students/api.ts` dagi `studentsApi` orqali `area: 'admin'` bilan so'raladi.
 */
export const STUDENTS_ENDPOINT = '/api/admin/students';

export const studentsApi = {
  list: (params: ListParams) =>
    api.get<Paged<Student>>(STUDENTS_ENDPOINT, { query: toQuery(params) }),
  detail: (id: string) => api.get<AdminStudentDetail>(`${STUDENTS_ENDPOINT}/${id}`),
};

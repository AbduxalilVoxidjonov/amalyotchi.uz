import { api } from '@/shared/api';
import { toQuery, type ListParams, type Paged } from '../shared/types';
import type { ImportResult } from '../shared/types';
import type { AdminStudentDetail, AssignCompanyInput, AssignCompanyResult, Student } from './types';

/**
 * Backend: `AdminStudentsController`.
 * GET  /api/admin/students?q=&page=&pageSize= → Paged<Student> (`q`: FISH, HEMIS ID, telefon, guruh)
 * GET  /api/admin/students/{id}               → AdminStudentDetail · 404
 * GET  /api/admin/students/import/template    → .xlsx shablon (Bearer talab qiladi — `downloadAuthFile`)
 * POST /api/admin/students/import             → StudentImportResult (multipart `file`) · 400
 * POST /api/admin/students/assign-company     → AssignCompanyResult · 400 · 404 · 409
 * Davomat va kundaliklar (`{id}/attendance`, `{id}/diaries`) — tyutor profilidagi bilan bir xil
 * shakl; ular `features/tutor/students/api.ts` dagi `studentsApi` orqali `area: 'admin'` bilan so'raladi.
 */
export const STUDENTS_ENDPOINT = '/api/admin/students';

/** Shablon `<a href>` bilan ochilmaydi (401) — `downloadAuthFile` token bilan yuklab oladi. */
export const STUDENTS_TEMPLATE_ENDPOINT = `${STUDENTS_ENDPOINT}/import/template`;
export const STUDENTS_IMPORT_ENDPOINT = `${STUDENTS_ENDPOINT}/import`;

/** Belgilangan talabalarni bitta korxonaga biriktirish (ommaviy amal). */
export const STUDENTS_ASSIGN_COMPANY_ENDPOINT = `${STUDENTS_ENDPOINT}/assign-company`;

/** Yuklab olinadigan shablon nomi (server `Content-Disposition` bermasa — zaxira). */
export const STUDENTS_TEMPLATE_FILE_NAME = 'talabalar-import-shablon.xlsx';

export const studentsApi = {
  list: (params: ListParams) =>
    api.get<Paged<Student>>(STUDENTS_ENDPOINT, { query: toQuery(params) }),
  detail: (id: string) => api.get<AdminStudentDetail>(`${STUDENTS_ENDPOINT}/${id}`),
  importExcel: (file: File) => {
    const body = new FormData();
    body.append('file', file);
    return api.post<ImportResult>(STUDENTS_IMPORT_ENDPOINT, body);
  },
  assignCompany: (input: AssignCompanyInput) =>
    api.post<AssignCompanyResult>(STUDENTS_ASSIGN_COMPANY_ENDPOINT, input),
};

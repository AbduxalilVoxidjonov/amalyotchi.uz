import { api } from '@/shared/api';
import { toQuery, type Paged } from '../shared/types';
import type { ImportResult } from '../shared/types';
import type {
  AdminStudentDetail,
  AssignCompanyInput,
  AssignCompanyResult,
  SetStudentCompanyInput,
  Student,
  StudentFilters,
  StudentListParams,
} from './types';

/**
 * Backend: `AdminStudentsController`.
 * GET  /api/admin/students?q=&page=&pageSize=&facultyId=&directionId=&course=
 *                                             → Paged<Student> (`q`: FISH, HEMIS ID, telefon, guruh;
 *                                               filtrlar ixtiyoriy, AND; `total` — filtrlangan)
 * GET  /api/admin/students/filters            → StudentFilters (fakultet/yo'nalish/kurs variantlari)
 * GET  /api/admin/students/{id}?periodId=     → AdminStudentDetail · 404 (talaba yoki begona davr)
 * GET  /api/admin/students/import/template    → .xlsx shablon (Bearer talab qiladi — `downloadAuthFile`)
 * POST /api/admin/students/import             → StudentImportResult (multipart `file`) · 400
 * POST /api/admin/students/assign-company     → AssignCompanyResult · 400 · 404 · 409
 * POST /api/admin/students/{id}/company       → AdminStudentDetail · 400 · 404 · 409
 *   (profildan bitta talabani biriktirish yoki boshqa korxonaga o'tkazish)
 * Davomat va kundaliklar (`{id}/attendance`, `{id}/diaries`) — tyutor profilidagi bilan bir xil
 * shakl; ular `features/tutor/students/api.ts` dagi `studentsApi` orqali `area: 'admin'` bilan so'raladi.
 */
export const STUDENTS_ENDPOINT = '/api/admin/students';

/** Shablon `<a href>` bilan ochilmaydi (401) — `downloadAuthFile` token bilan yuklab oladi. */
/** Filtr variantlari (fakultetlar, yo'nalishlar, kurslar). */
export const STUDENTS_FILTERS_ENDPOINT = `${STUDENTS_ENDPOINT}/filters`;

/**
 * Filtr variantlari kaliti. `adminKeys.studentsAll()` prefiksi ostida — import/biriktirishdan keyingi
 * invalidate ham qamraydi; fakultet/yo'nalish/guruh mutatsiyalari uni alohida invalidate qiladi.
 */
export const studentFiltersKey = () => ['admin', 'students', 'filters'] as const;

export const STUDENTS_TEMPLATE_ENDPOINT = `${STUDENTS_ENDPOINT}/import/template`;
export const STUDENTS_IMPORT_ENDPOINT = `${STUDENTS_ENDPOINT}/import`;

/** Belgilangan talabalarni bitta korxonaga biriktirish (ommaviy amal). */
export const STUDENTS_ASSIGN_COMPANY_ENDPOINT = `${STUDENTS_ENDPOINT}/assign-company`;

/** Profildan bitta talabani korxonaga biriktirish / o'tkazish. */
export const studentCompanyEndpoint = (id: string) =>
  `${STUDENTS_ENDPOINT}/${encodeURIComponent(id)}/company`;

/** Yuklab olinadigan shablon nomi (server `Content-Disposition` bermasa — zaxira). */
export const STUDENTS_TEMPLATE_FILE_NAME = 'talabalar-import-shablon.xlsx';

export const studentsApi = {
  list: ({ facultyId, directionId, course, ...params }: StudentListParams) =>
    api.get<Paged<Student>>(STUDENTS_ENDPOINT, {
      query: {
        ...toQuery(params),
        facultyId: facultyId || undefined,
        directionId: directionId || undefined,
        course,
      },
    }),
  filters: () => api.get<StudentFilters>(STUDENTS_FILTERS_ENDPOINT),
  detail: (id: string, periodId: string | null = null) =>
    api.get<AdminStudentDetail>(`${STUDENTS_ENDPOINT}/${id}`, { query: { periodId } }),
  importExcel: (file: File) => {
    const body = new FormData();
    body.append('file', file);
    return api.post<ImportResult>(STUDENTS_IMPORT_ENDPOINT, body);
  },
  assignCompany: (input: AssignCompanyInput) =>
    api.post<AssignCompanyResult>(STUDENTS_ASSIGN_COMPANY_ENDPOINT, input),
  setCompany: (id: string, input: SetStudentCompanyInput) =>
    api.post<AdminStudentDetail>(studentCompanyEndpoint(id), input),
};

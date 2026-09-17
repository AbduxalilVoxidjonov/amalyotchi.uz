import { api } from '@/shared/api';
import { toQuery, type ImportResult, type ListParams, type Paged } from '../shared/types';
import type { Company, CompanyDetail, CompanyInput, CompanyStudent } from './types';

/**
 * Backend: `AdminCompaniesController`.
 * GET    /api/admin/companies?q=&page=&pageSize= → Paged<Company> (`q`: nom, STIR, manzil)
 * GET    /api/admin/companies/{id}              → CompanyDetail · 404
 * GET    /api/admin/companies/{id}/students     → CompanyStudent[] · 404
 * POST   /api/admin/companies                   → 201 CompanyDetail · 400 · 409 (STIR band)
 * PUT    /api/admin/companies/{id}              → 200 CompanyDetail · 400 · 404 · 409
 * PATCH  /api/admin/companies/{id}/status       → 200 CompanyDetail · 404
 * DELETE /api/admin/companies/{id}              → 204 · 404 · 409 (faol yoki talabasi bor)
 * GET    /api/admin/companies/import/template   → .xlsx shablon (Bearer talab qiladi)
 * POST   /api/admin/companies/import            → ImportResult (multipart `file`) · 400
 */
export const COMPANIES_ENDPOINT = '/api/admin/companies';

/** Shablon `<a href>` bilan ochilmaydi (401) — `downloadAuthFile` token bilan yuklab oladi. */
export const COMPANIES_TEMPLATE_ENDPOINT = `${COMPANIES_ENDPOINT}/import/template`;
export const COMPANIES_IMPORT_ENDPOINT = `${COMPANIES_ENDPOINT}/import`;

/** Yuklab olinadigan shablon nomi (server `Content-Disposition` bermasa — zaxira). */
export const COMPANIES_TEMPLATE_FILE_NAME = 'korxonalar-import-shablon.xlsx';

export const companiesApi = {
  list: (params: ListParams) =>
    api.get<Paged<Company>>(COMPANIES_ENDPOINT, { query: toQuery(params) }),
  detail: (id: string) => api.get<CompanyDetail>(`${COMPANIES_ENDPOINT}/${id}`),
  students: (id: string) => api.get<CompanyStudent[]>(`${COMPANIES_ENDPOINT}/${id}/students`),
  create: (body: CompanyInput) => api.post<CompanyDetail>(COMPANIES_ENDPOINT, body),
  update: (id: string, body: CompanyInput) =>
    api.put<CompanyDetail>(`${COMPANIES_ENDPOINT}/${id}`, body),
  setStatus: (id: string, isActive: boolean) =>
    api.patch<CompanyDetail>(`${COMPANIES_ENDPOINT}/${id}/status`, { isActive }),
  remove: (id: string) => api.delete<void>(`${COMPANIES_ENDPOINT}/${id}`),
  importExcel: (file: File) => {
    const body = new FormData();
    body.append('file', file);
    return api.post<ImportResult>(COMPANIES_IMPORT_ENDPOINT, body);
  },
};

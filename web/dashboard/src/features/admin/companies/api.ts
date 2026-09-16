import { api } from '@/shared/api';
import { toQuery, type ListParams, type Paged } from '../shared/types';
import type { Company, CompanyDetail, CompanyStudent } from './types';

/**
 * Backend: `AdminCompaniesController`.
 * GET /api/admin/companies?q=&page=&pageSize= → Paged<Company> (`q`: nom, STIR, manzil)
 * GET /api/admin/companies/{id}              → CompanyDetail · 404
 * GET /api/admin/companies/{id}/students     → CompanyStudent[] · 404
 */
export const COMPANIES_ENDPOINT = '/api/admin/companies';

export const companiesApi = {
  list: (params: ListParams) =>
    api.get<Paged<Company>>(COMPANIES_ENDPOINT, { query: toQuery(params) }),
  detail: (id: string) => api.get<CompanyDetail>(`${COMPANIES_ENDPOINT}/${id}`),
  students: (id: string) => api.get<CompanyStudent[]>(`${COMPANIES_ENDPOINT}/${id}/students`),
};

import { api } from '@/shared/api';
import { toQuery, type ListParams, type Paged } from '../shared/types';
import type { Company } from './types';

/** Backend: `AdminCompaniesController`. GET /api/admin/companies?q=&page=&pageSize= → Paged<Company> (`q`: nom, STIR, manzil). */
export const COMPANIES_ENDPOINT = '/api/admin/companies';

export const companiesApi = {
  list: (params: ListParams) =>
    api.get<Paged<Company>>(COMPANIES_ENDPOINT, { query: toQuery(params) }),
};

import { api } from '@/shared/api';
import type { CompanyStudent, TutorCompany, TutorCompanyDetail } from './types';

/**
 * Tyutor · Korxonalar — `TutorCompaniesController` (ko'lam bo'yicha filtrlangan):
 * GET /api/tutor/companies              → TutorCompany[]
 * GET /api/tutor/companies/{id}         → CompanyDetail · 404 (ko'lamda talabasi yo'q bo'lsa)
 * GET /api/tutor/companies/{id}/students→ CompanyStudent[] · 404
 */
export const TUTOR_COMPANIES_ENDPOINT = '/api/tutor/companies';

export const companiesApi = {
  list: () => api.get<TutorCompany[]>(TUTOR_COMPANIES_ENDPOINT),
  detail: (id: string) => api.get<TutorCompanyDetail>(`${TUTOR_COMPANIES_ENDPOINT}/${id}`),
  students: (id: string) => api.get<CompanyStudent[]>(`${TUTOR_COMPANIES_ENDPOINT}/${id}/students`),
};

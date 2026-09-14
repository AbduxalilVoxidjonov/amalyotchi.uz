import { api } from '@/shared/api';
import type { ReportsCatalog } from './types';

/**
 * `ReportsController` (Admin, Tyutor — ko'lam Bearer'dan):
 *   GET /api/reports                → ReportsCatalog
 *   GET /api/reports/{id}/download  → fayl (keyingi bosqich, M14 — hozircha `available: false`)
 */
export const REPORTS_ENDPOINT = '/api/reports';

export const reportsApi = {
  catalog: () => api.get<ReportsCatalog>(REPORTS_ENDPOINT),
};

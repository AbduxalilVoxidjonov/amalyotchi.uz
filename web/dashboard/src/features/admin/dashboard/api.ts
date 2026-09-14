import { api } from '@/shared/api';
import type { AdminDashboard } from './types';

/** Backend: `AdminDashboardController` (AdminOnly). GET /api/admin/dashboard → AdminDashboard. */
export const ADMIN_DASHBOARD_ENDPOINT = '/api/admin/dashboard';

export const dashboardApi = {
  get: () => api.get<AdminDashboard>(ADMIN_DASHBOARD_ENDPOINT),
};

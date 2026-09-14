import { api } from '@/shared/api';
import type { AdminSettings, SettingsUpdate } from './types';

/**
 * Backend: `AdminSettingsController` (AdminOnly).
 *   GET /api/admin/settings                      → AdminSettings
 *   PUT /api/admin/settings { values: {key: v} } → 200 AdminSettings | 400 ProblemDetails `errors{key:[...]}`
 */
export const SETTINGS_ENDPOINT = '/api/admin/settings';

export const settingsApi = {
  get: () => api.get<AdminSettings>(SETTINGS_ENDPOINT),
  update: (body: SettingsUpdate) => api.put<AdminSettings>(SETTINGS_ENDPOINT, body),
};

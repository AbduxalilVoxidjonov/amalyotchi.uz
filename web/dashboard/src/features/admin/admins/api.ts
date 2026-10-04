import { api } from '@/shared/api';
import type {
  AdminAccount,
  AdminCreateInput,
  AdminCreateResult,
  AdminListResponse,
  AdminPasswordResetResult,
  AdminUpdateInput,
} from './types';

/** Kontrakt — `types.ts` izohida (v3.28, faqat bosh admin). */
export const ADMINS_ENDPOINT = '/api/admin/admins';

const adminUrl = (id: string) => `${ADMINS_ENDPOINT}/${encodeURIComponent(id)}`;

export const adminsApi = {
  list: () => api.get<AdminListResponse>(ADMINS_ENDPOINT),
  create: (body: AdminCreateInput) => api.post<AdminCreateResult>(ADMINS_ENDPOINT, body),
  update: (id: string, body: AdminUpdateInput) => api.put<AdminAccount>(adminUrl(id), body),
  setStatus: (id: string, isActive: boolean) =>
    api.patch<AdminAccount>(`${adminUrl(id)}/status`, { isActive }),
  resetPassword: (id: string) => api.post<AdminPasswordResetResult>(`${adminUrl(id)}/password`),
};

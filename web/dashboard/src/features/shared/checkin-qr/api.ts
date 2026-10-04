import { api } from '@/shared/api';
import type { CompanyCheckInQr } from './types';

/**
 * GET  /api/admin/companies/{id}/checkin-qr        → CompanyCheckInQr · 404
 * POST /api/admin/companies/{id}/checkin-qr/rotate → CompanyCheckInQr (eski payload bekor)
 */
export function checkinQrEndpoint(companyId: string): string {
  return `/api/admin/companies/${companyId}/checkin-qr`;
}

export const checkinQrApi = {
  get: (companyId: string) => api.get<CompanyCheckInQr>(checkinQrEndpoint(companyId)),
  rotate: (companyId: string) =>
    api.post<CompanyCheckInQr>(`${checkinQrEndpoint(companyId)}/rotate`),
};

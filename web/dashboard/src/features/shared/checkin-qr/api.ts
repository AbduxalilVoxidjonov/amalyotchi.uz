import { api } from '@/shared/api';
import type { CheckInQrArea, CompanyCheckInQr } from './types';

/**
 * GET  /api/{admin|tutor}/companies/{id}/checkin-qr        → CompanyCheckInQr · 404
 * POST /api/{admin|tutor}/companies/{id}/checkin-qr/rotate → CompanyCheckInQr (eski payload bekor)
 * Tyutor uchun ko'lamdan tashqari korxona → 404.
 */
export function checkinQrEndpoint(area: CheckInQrArea, companyId: string): string {
  return `/api/${area}/companies/${companyId}/checkin-qr`;
}

export const checkinQrApi = {
  get: (area: CheckInQrArea, companyId: string) =>
    api.get<CompanyCheckInQr>(checkinQrEndpoint(area, companyId)),
  rotate: (area: CheckInQrArea, companyId: string) =>
    api.post<CompanyCheckInQr>(`${checkinQrEndpoint(area, companyId)}/rotate`),
};

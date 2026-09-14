import { http, HttpResponse, type HttpHandler } from 'msw';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import { requireBearer } from '@/mocks/problem';
import type { PracticePlaceDto } from './types';

/** SPEC-SCREENS §14 mock — kontrakt v2 shakli. */
export const mockPlace: PracticePlaceDto = {
  status: 'approved',
  comment: null,
  company: 'Tech Solutions MChJ',
  tin: '304512889',
  activity: "Dasturiy ta'minot ishlab chiqish",
  address: "Toshkent, Amir Temur ko'chasi 108",
  supervisorName: 'Islomov B.',
  supervisorPhone: '+998901234567',
  mentorName: 'Xolmatov S.',
  mentorPhone: '+998935551209',
  radiusM: 150,
  lat: 41.3111,
  lng: 69.2797,
  periodFrom: '2026-10-01',
  periodTo: '2026-11-15',
  contract: {
    fileId: '01a0a0e0-0000-7000-8000-000000000001',
    fileName: 'shartnoma_aliyev.pdf',
    pages: 2,
    sizeBytes: 1_887_436,
    uploadedAt: '2026-09-24T09:12:00+05:00',
    approvedAt: '2026-10-08T14:03:00+05:00',
    approvedBy: 'N. Saidova',
    templateUrl: '/files/shartnoma-shablon.docx',
  },
};

export const placeHandlers: HttpHandler[] = [
  http.get(STUDENT_ENDPOINTS.place, ({ request }) => {
    return requireBearer(request) ?? HttpResponse.json(mockPlace);
  }),
];

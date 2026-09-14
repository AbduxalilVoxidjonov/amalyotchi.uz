import { http, HttpResponse, type HttpHandler } from 'msw';
import { paginateMock } from '../shared/paginate';
import { TUTORS_ENDPOINT } from './api';
import type { Tutor } from './types';

/** Backend `TutorRow` shaklida (SPEC-SCREENS §9.5 raqamlari). */
export const mockTutors: Tutor[] = [
  {
    id: 't1',
    fullName: 'Nodira Saidova',
    phone: '+998901112233',
    facultyId: 'f1',
    facultyCode: 'AT',
    facultyName: 'Axborot texnologiyalari',
    groups: ['412-22', '413-22'],
    students: 38,
    pending: 0,
    oldestPendingAt: null,
    avgDecisionHours: 4,
    lastActiveAt: '2026-10-12T04:31:00+00:00',
    isActive: true,
    status: 'active',
  },
  {
    id: 't2',
    fullName: 'Baxtiyor Rasulov',
    phone: '+998912445102',
    facultyId: 'f2',
    facultyCode: 'IM',
    facultyName: 'Iqtisodiyot va moliya',
    groups: ['221-23', '222-23', '223-23', '321-22', '322-22'],
    students: 118,
    pending: 7,
    oldestPendingAt: '2026-10-08T04:00:00+00:00',
    avgDecisionHours: 84,
    lastActiveAt: '2026-10-11T13:10:00+00:00',
    isActive: true,
    status: 'late',
  },
  {
    id: 't3',
    fullName: 'Dilshod Ergashev',
    phone: '+998937001845',
    facultyId: 'f3',
    facultyCode: 'QA',
    facultyName: 'Qurilish va arxitektura',
    groups: ['318-21', '319-21', '320-21'],
    students: 64,
    pending: 2,
    oldestPendingAt: '2026-10-11T06:30:00+00:00',
    avgDecisionHours: 24,
    lastActiveAt: '2026-10-12T03:00:00+00:00',
    isActive: true,
    status: 'active',
  },
  {
    id: 't4',
    fullName: "Gulnora Yo'ldosheva",
    phone: '+998975126330',
    facultyId: 'f4',
    facultyCode: 'FL',
    facultyName: 'Filologiya',
    groups: ['101-24', '102-24', '201-23', '202-23'],
    students: 82,
    pending: 1,
    oldestPendingAt: '2026-10-12T02:15:00+00:00',
    avgDecisionHours: 6,
    lastActiveAt: null,
    isActive: true,
    status: 'active',
  },
];

export const tutorsHandlers: HttpHandler[] = [
  http.get(TUTORS_ENDPOINT, ({ request }) =>
    HttpResponse.json(
      paginateMock(request.url, mockTutors, (t) => [
        t.fullName,
        t.phone,
        t.facultyCode,
        t.facultyName,
      ]),
    ),
  ),
];

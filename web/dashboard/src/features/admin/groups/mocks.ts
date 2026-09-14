import { http, HttpResponse, type HttpHandler } from 'msw';
import { paginateMock } from '../shared/paginate';
import { GROUPS_ENDPOINT } from './api';
import type { Group, GroupPeriod } from './types';

const PERIOD: GroupPeriod = {
  id: 'p1',
  name: 'Ishlab chiqarish amaliyoti 2026',
  status: 'active',
  startDate: '2026-08-31',
  endDate: '2026-10-14',
};

/** Backend `GroupRow` shaklida (SPEC-SCREENS §9.4 raqamlari). */
export const mockGroups: Group[] = [
  {
    id: 'g1',
    code: '412-22',
    course: 3,
    direction: 'Dasturiy injiniring',
    faculty: 'Axborot texnologiyalari',
    facultyCode: 'AT',
    tutorId: 't1',
    tutor: 'Nodira Saidova',
    students: 19,
    attendancePct: 91,
    period: PERIOD,
  },
  {
    id: 'g2',
    code: '413-22',
    course: 3,
    direction: 'Kompyuter injiniringi',
    faculty: 'Axborot texnologiyalari',
    facultyCode: 'AT',
    tutorId: 't1',
    tutor: 'Nodira Saidova',
    students: 19,
    attendancePct: 88,
    period: PERIOD,
  },
  {
    id: 'g3',
    code: '221-23',
    course: 2,
    direction: 'Bank ishi',
    faculty: 'Iqtisodiyot va moliya',
    facultyCode: 'IM',
    tutorId: 't2',
    tutor: 'Baxtiyor Rasulov',
    students: 24,
    attendancePct: 79,
    period: PERIOD,
  },
  {
    id: 'g4',
    code: '318-21',
    course: 4,
    direction: 'Qurilish muhandisligi',
    faculty: 'Qurilish va arxitektura',
    facultyCode: 'QA',
    tutorId: null,
    tutor: null,
    students: 22,
    attendancePct: 0,
    period: null,
  },
];

export const groupsHandlers: HttpHandler[] = [
  http.get(GROUPS_ENDPOINT, ({ request }) =>
    HttpResponse.json(
      paginateMock(request.url, mockGroups, (g) => [g.code, g.direction, g.faculty, g.tutor]),
    ),
  ),
];

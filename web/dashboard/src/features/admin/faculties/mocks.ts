import { http, HttpResponse, type HttpHandler } from 'msw';
import { paginateMock } from '../shared/paginate';
import { FACULTIES_ENDPOINT } from './api';
import type { Faculty } from './types';

/** Backend `FacultyRow` shaklida (SPEC-SCREENS §9.3 raqamlari). */
export const mockFaculties: Faculty[] = [
  {
    id: 'f1',
    name: 'Axborot texnologiyalari',
    code: 'AT',
    directions: 4,
    groups: 12,
    students: 286,
    tutors: 3,
    attendancePct: 94,
    status: 'active',
  },
  {
    id: 'f2',
    name: 'Iqtisodiyot va moliya',
    code: 'IM',
    directions: 6,
    groups: 15,
    students: 341,
    tutors: 4,
    attendancePct: 89,
    status: 'active',
  },
  {
    id: 'f3',
    name: 'Qurilish va arxitektura',
    code: 'QA',
    directions: 3,
    groups: 9,
    students: 198,
    tutors: 2,
    attendancePct: 82,
    status: 'active',
  },
  {
    id: 'f4',
    name: 'Filologiya',
    code: 'FL',
    directions: 2,
    groups: 6,
    students: 124,
    tutors: 2,
    attendancePct: 76,
    status: 'attention',
  },
];

export const facultiesHandlers: HttpHandler[] = [
  http.get(FACULTIES_ENDPOINT, ({ request }) =>
    HttpResponse.json(paginateMock(request.url, mockFaculties, (f) => [f.name, f.code])),
  ),
];

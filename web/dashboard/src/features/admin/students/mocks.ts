import { http, HttpResponse, type HttpHandler } from 'msw';
import { paginateMock } from '../shared/paginate';
import { STUDENTS_ENDPOINT } from './api';
import type { Student } from './types';

/** Backend `StudentRow` shaklida (SPEC-SCREENS §9.6 raqamlari). */
export const mockStudents: Student[] = [
  {
    id: 's1',
    fullName: 'Aliyev Akmal',
    hemisId: '341030',
    groupId: 'g1',
    group: '412-22',
    course: 3,
    faculty: 'Axborot texnologiyalari',
    company: 'Tech Solutions MChJ',
    attendancePct: 94,
    suspiciousDays: 0,
    telegramLinked: true,
    status: 'active',
  },
  {
    id: 's2',
    fullName: 'Sobirov Diyor',
    hemisId: '341061',
    groupId: 'g2',
    group: '413-22',
    course: 3,
    faculty: 'Axborot texnologiyalari',
    company: 'Uzinfocom',
    attendancePct: 64,
    suspiciousDays: 3,
    telegramLinked: true,
    status: 'flagged',
  },
  {
    id: 's3',
    fullName: 'Ismoilova Madina',
    hemisId: '342014',
    groupId: 'g3',
    group: '221-23',
    course: 2,
    faculty: 'Iqtisodiyot va moliya',
    company: 'Ipak Yuli Bank',
    attendancePct: 87,
    suspiciousDays: 0,
    telegramLinked: true,
    status: 'active',
  },
  {
    id: 's4',
    fullName: 'Nazarov Firdavs',
    hemisId: '340077',
    groupId: 'g4',
    group: '318-21',
    course: 4,
    faculty: 'Qurilish va arxitektura',
    company: 'Qurilish Trest 12',
    attendancePct: 81,
    suspiciousDays: 1,
    telegramLinked: true,
    status: 'active',
  },
  {
    id: 's5',
    fullName: 'Oripov Javohir',
    hemisId: '342020',
    groupId: 'g3',
    group: '221-23',
    course: 2,
    faculty: 'Iqtisodiyot va moliya',
    company: null,
    attendancePct: 0,
    suspiciousDays: 0,
    telegramLinked: false,
    status: 'unlinked',
  },
];

export const studentsHandlers: HttpHandler[] = [
  http.get(STUDENTS_ENDPOINT, ({ request }) =>
    HttpResponse.json(
      paginateMock(request.url, mockStudents, (s) => [s.fullName, s.hemisId, s.group, s.faculty]),
    ),
  ),
];

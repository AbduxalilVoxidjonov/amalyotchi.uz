import { http, HttpResponse, type HttpHandler } from 'msw';
import { problem } from '@/mocks/data';
import {
  buildAttendance,
  buildDetail,
  buildDiaries,
  mockStudents as tutorMockStudents,
} from '@/features/tutor/students/mocks';
import { paginateMock } from '../shared/paginate';
import { STUDENTS_ENDPOINT } from './api';
import type { AdminStudentDetail, AdminStudentTutor, Student } from './types';

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

/* ────────────────────────────────────────────────────────────────────────────
 * Talaba profili mock'i. Bloklar tyutor mock'idan quriladi (backend'da ham ayni
 * handler), admin qatori esa unga nom/guruh/fakultet va admin maydonlarini beradi.
 * ──────────────────────────────────────────────────────────────────────────── */

/** Admin qatori → tyutor mock profili (tartib bo'yicha, doimiy juftlik). */
const SOURCE_PROFILE_ID: Record<string, string> = {
  s1: 's-341030',
  s2: 's-341032',
  s3: 's-341031',
  s4: 's-341034',
  s5: 's-341033',
};

const DEPARTMENTS: Record<string, string> = {
  'Axborot texnologiyalari': 'Dasturiy injiniring kafedrasi',
  'Iqtisodiyot va moliya': 'Moliya va bank ishi kafedrasi',
  'Qurilish va arxitektura': 'Qurilish muhandisligi kafedrasi',
};

const TUTORS: Record<string, AdminStudentTutor> = {
  'Axborot texnologiyalari': { id: 't1', fullName: 'Nodira Saidova', phone: '+998907654321' },
  'Iqtisodiyot va moliya': { id: 't2', fullName: 'Baxtiyor Rasulov', phone: '+998912445102' },
  'Qurilish va arxitektura': { id: 't3', fullName: 'Dilshod Ergashev', phone: null },
};

const notFound = () =>
  HttpResponse.json(problem(404, 'Topilmadi', 'Talaba topilmadi.'), { status: 404 });

/** Admin mock qatori uchun manba profil id'si (topilmasa — null). */
function sourceId(adminId: string): string | null {
  const row = mockStudents.find((s) => s.id === adminId);
  if (!row) return null;
  return SOURCE_PROFILE_ID[adminId] ?? tutorMockStudents[0]!.id;
}

function buildAdminDetail(adminId: string): AdminStudentDetail | null {
  const row = mockStudents.find((s) => s.id === adminId);
  const source = sourceId(adminId);
  const base = source ? buildDetail(source) : null;
  if (!row || !base) return null;

  return {
    ...base,
    id: row.id,
    name: row.fullName,
    hemisId: row.hemisId,
    group: row.group,
    course: row.course,
    faculty: row.faculty,
    groupId: row.groupId,
    department: DEPARTMENTS[row.faculty] ?? "Umumiy kafedra",
    adminStatus: row.status,
    telegramLinked: row.telegramLinked,
    tutor: TUTORS[row.faculty] ?? null,
  };
}

export const studentsHandlers: HttpHandler[] = [
  http.get(`${STUDENTS_ENDPOINT}/:id/attendance`, ({ params, request }) => {
    const source = sourceId(String(params['id']));
    if (!source) return notFound();
    const url = new URL(request.url);
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    const days = buildAttendance(source).filter(
      (d) => (!from || d.date >= from) && (!to || d.date <= to),
    );
    return HttpResponse.json(days);
  }),

  http.get(`${STUDENTS_ENDPOINT}/:id/diaries`, ({ params }) => {
    const source = sourceId(String(params['id']));
    return source ? HttpResponse.json(buildDiaries(source)) : notFound();
  }),

  http.get(`${STUDENTS_ENDPOINT}/:id`, ({ params }) => {
    const detail = buildAdminDetail(String(params['id']));
    return detail ? HttpResponse.json(detail) : notFound();
  }),

  http.get(STUDENTS_ENDPOINT, ({ request }) =>
    HttpResponse.json(
      paginateMock(request.url, mockStudents, (s) => [s.fullName, s.hemisId, s.group, s.faculty]),
    ),
  ),
];

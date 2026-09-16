import { http, HttpResponse, type HttpHandler } from 'msw';
import { TUTOR_COMPANIES_ENDPOINT } from './api';
import type {
  CompanyPeriod,
  CompanyStudent,
  CompanyStudentState,
  TutorCompany,
  TutorCompanyDetail,
} from './types';

const PROBLEM_HEADERS = { 'Content-Type': 'application/problem+json' };

function notFound() {
  return HttpResponse.json(
    { status: 404, title: 'Topilmadi', detail: 'Korxona topilmadi.' },
    { status: 404, headers: PROBLEM_HEADERS },
  );
}

/** `maxStudentsPerCompany` sozlamasining demo qiymati (backend default — 10). */
export const MOCK_MAX_STUDENTS = 10;

const PERIOD_AUTUMN: CompanyPeriod = {
  id: 'p1',
  name: '3-kurs kuzgi amaliyot',
  startDate: '2026-09-01',
  endDate: '2026-10-31',
  students: 0,
};

/** Tyutor ko'lamidagi korxonalar. */
const COMPANIES: TutorCompany[] = [
  {
    id: 'c3',
    name: 'Qurilish Trest 12',
    tin: '305881204',
    address: 'Toshkent v., Zangiota',
    lat: 41.2069,
    lng: 69.1432,
    radiusM: 450,
    students: 2,
    totalStudents: 9,
    maxStudents: MOCK_MAX_STUDENTS,
    overLimit: false,
    attendancePct: 76.5,
    suspiciousDays: 1,
    flag: 'largeRadius',
  },
  {
    id: 'c1',
    name: 'Tech Solutions MChJ',
    tin: '304512889',
    address: 'Toshkent, Amir Temur 108',
    lat: 41.3111,
    lng: 69.2797,
    radiusM: 150,
    students: 3,
    totalStudents: 4,
    maxStudents: MOCK_MAX_STUDENTS,
    overLimit: false,
    attendancePct: 91.5,
    suspiciousDays: 0,
    flag: null,
  },
  {
    // STIR nazorati namunasi: ko'lamda 5 talaba, tizimda 21 — chegara 10.
    id: 'c4',
    name: 'Mega Servis MChJ',
    tin: '308190556',
    address: 'Toshkent, Chilonzor 7',
    lat: 41.2755,
    lng: 69.2033,
    radiusM: 200,
    students: 5,
    totalStudents: 21,
    maxStudents: MOCK_MAX_STUDENTS,
    overLimit: true,
    attendancePct: 68.4,
    suspiciousDays: 2,
    flag: 'tooManyStudents',
  },
  {
    id: 'c5',
    name: "Ipak Yo'li Logistika",
    tin: '302774610',
    address: 'Toshkent v., Qibray',
    lat: 41.3892,
    lng: 69.4211,
    radiusM: 260,
    students: 2,
    totalStudents: 5,
    maxStudents: MOCK_MAX_STUDENTS,
    overLimit: false,
    attendancePct: 58.0,
    suspiciousDays: 12,
    flag: 'suspicious',
  },
];

/** Nom bo'yicha tartib — backend shunday qaytaradi. */
export const mockTutorCompanies: TutorCompany[] = [...COMPANIES].sort((a, b) =>
  a.name.localeCompare(b.name, 'uz'),
);

interface DetailExtra {
  activity: string;
  supervisorName: string;
  supervisorPhone: string;
  mentorName: string | null;
  mentorPhone: string | null;
  isActive: boolean;
  periods: CompanyPeriod[];
}

const EXTRA: Record<string, DetailExtra> = {
  c1: {
    activity: "Dasturiy ta'minot",
    supervisorName: 'Rustamov Jasur',
    supervisorPhone: '+998901234567',
    mentorName: 'Xolmatov Aziz',
    mentorPhone: '+998935557711',
    isActive: true,
    periods: [{ ...PERIOD_AUTUMN, students: 3 }],
  },
  c3: {
    activity: 'Qurilish',
    supervisorName: 'Nazarov Otabek',
    supervisorPhone: '+998971110099',
    mentorName: null,
    mentorPhone: null,
    isActive: true,
    periods: [{ ...PERIOD_AUTUMN, students: 2 }],
  },
  c4: {
    activity: "Xizmat ko'rsatish",
    supervisorName: 'Ergashev Sanjar',
    supervisorPhone: '+998903334455',
    mentorName: 'Qodirova Malika',
    mentorPhone: '+998995556677',
    isActive: true,
    periods: [
      { ...PERIOD_AUTUMN, students: 4 },
      {
        id: 'p2',
        name: '2-kurs bahorgi amaliyot',
        startDate: '2026-02-10',
        endDate: '2026-04-10',
        students: 1,
      },
    ],
  },
  c5: {
    activity: 'Logistika',
    supervisorName: 'Toshpulatov Ulugbek',
    supervisorPhone: '+998912223344',
    mentorName: null,
    mentorPhone: null,
    isActive: false,
    periods: [{ ...PERIOD_AUTUMN, students: 2 }],
  },
};

export function mockTutorCompanyDetail(id: string): TutorCompanyDetail | null {
  const row = mockTutorCompanies.find((c) => c.id === id);
  const extra = EXTRA[id];
  if (!row || !extra) return null;
  return {
    id: row.id,
    name: row.name,
    tin: row.tin,
    activity: extra.activity,
    address: row.address,
    lat: row.lat,
    lng: row.lng,
    radiusM: row.radiusM,
    supervisorName: extra.supervisorName,
    supervisorPhone: extra.supervisorPhone,
    mentorName: extra.mentorName,
    mentorPhone: extra.mentorPhone,
    isActive: extra.isActive,
    students: row.students,
    totalStudents: row.totalStudents,
    suspiciousDays: row.suspiciousDays,
    maxStudents: row.maxStudents,
    overLimit: row.overLimit,
    flag: row.flag,
    periods: extra.periods,
  };
}

const NAMES: Record<string, readonly string[]> = {
  c1: ['Aliyev Akmal', 'Karimov Bekzod', 'Yusupova Nilufar'],
  c3: ['Rahimov Sardor', 'Sobirov Diyor'],
  c4: [
    'Abdullayev Doston',
    'Ismoilova Kamola',
    'Jalilov Nodir',
    'Maxmudova Sevara',
    'Olimov Farrux',
  ],
  c5: ['Toshpulatova Zarina', 'Vahobov Sardor'],
};

const GROUPS = ['412-22', '413-22'];

function stateOf(pct: number, suspicious: number): CompanyStudentState {
  if (pct < 70) return 'redFlag';
  return suspicious > 0 ? 'suspicious' : 'active';
}

/** Deterministik demo talabalar (ko'lamdagilar). */
export function mockTutorCompanyStudents(companyId: string): CompanyStudent[] {
  const names = NAMES[companyId] ?? [];
  const detail = mockTutorCompanyDetail(companyId);
  const periods = detail?.periods ?? [];
  const totalDays = 36;
  const hemisBase = 341000 + (mockTutorCompanies.findIndex((c) => c.id === companyId) + 1) * 100;
  return names
    .map((name, i) => {
      const pct = [94, 86, 64, 100, 78][i % 5] ?? 80;
      const suspicious = i % 5 === 4 ? 3 : 0;
      const period = periods[i % Math.max(1, periods.length)] ?? null;
      return {
        studentId: `${companyId}-s${i + 1}`,
        name,
        hemisId: String(hemisBase + i),
        group: GROUPS[i % GROUPS.length] ?? '412-22',
        course: 3,
        faculty: 'Axborot texnologiyalari',
        tutorName: 'Nodira Saidova',
        applicationStatus: 'approved',
        periodName: period?.name ?? null,
        attendancePct: pct,
        attendedDays: Math.round((pct / 100) * totalDays),
        totalDays,
        diaryCount: Math.max(0, Math.round((pct / 100) * totalDays) - (i % 4)),
        state: stateOf(pct, suspicious),
        suspiciousCount: suspicious,
      } satisfies CompanyStudent;
    })
    .sort((a, b) => a.name.localeCompare(b.name, 'uz'));
}

export const companiesHandlers: HttpHandler[] = [
  http.get(`${TUTOR_COMPANIES_ENDPOINT}/:id/students`, ({ params }) => {
    const id = String(params['id']);
    if (!mockTutorCompanies.some((c) => c.id === id)) return notFound();
    return HttpResponse.json(mockTutorCompanyStudents(id));
  }),
  http.get(`${TUTOR_COMPANIES_ENDPOINT}/:id`, ({ params }) => {
    const detail = mockTutorCompanyDetail(String(params['id']));
    if (!detail) return notFound();
    return HttpResponse.json(detail);
  }),
  http.get(TUTOR_COMPANIES_ENDPOINT, () => HttpResponse.json(mockTutorCompanies)),
];

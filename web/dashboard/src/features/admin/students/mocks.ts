import { hasMockStudentPassword } from '@/features/shared/student-password/passwordStore';
import { http, HttpResponse, type HttpHandler } from 'msw';
import { problem } from '@/mocks/data';
import {
  buildDetail,
  mockAttendanceResponse,
  mockDiariesResponse,
  mockStudents as tutorMockStudents,
  periodNotFound,
} from '@/features/tutor/students/mocks';
import { mockCompanies } from '../companies/mocks';
import { mockDirections } from '../faculties/directions/mocks';
import { mockGroups } from '../faculties/groups/mocks';
import { mockFaculties } from '../faculties/mocks';
import { problemResponse } from '../shared/mockProblem';
import { paginateMock } from '../shared/paginate';
import type { ImportResult } from '../shared/types';
import type {
  ActiveCompanyRef,
  StudentApplication,
  StudentCompany,
  StudentPeriodOption,
} from '@/features/tutor/students/types';
import type { Company } from '../companies/types';
import {
  STUDENTS_ASSIGN_COMPANY_ENDPOINT,
  STUDENTS_ENDPOINT,
  STUDENTS_FILTERS_ENDPOINT,
  STUDENTS_IMPORT_ENDPOINT,
  STUDENTS_TEMPLATE_ENDPOINT,
  STUDENTS_TEMPLATE_FILE_NAME,
} from './api';
import type {
  AdminStudentDetail,
  AdminStudentTutor,
  AssignCompanyError,
  AssignCompanyInput,
  SetStudentCompanyInput,
  Student,
  StudentFilters,
} from './types';
import { STUDENT_COMPANY_COMMENT_MAX } from './types';

/**
 * Backend `StudentRow` shaklida (SPEC-SCREENS §9.6 raqamlari). `company` — faqat aktiv korxona va
 * profildagi `activeCompany.name` bilan bir xil (manba tyutor profili bo'yicha).
 */
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
    // Yopilgan yozgi davrda «Uzinfocom» da bo'lgan, hozir aktiv korxonasi yo'q → null.
    company: null,
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
    company: 'Agrobank ATB',
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

/* ────────────────────────────────────────────────────────────────────────────
 * Profildan biriktirish / o'tkazish (`POST /students/{id}/company`) — mock holati.
 * Talaba → yangi korxona + yaratilgan ariza. Testlar orasida `resetStudentCompanyMock()`.
 * ──────────────────────────────────────────────────────────────────────────── */

interface CompanyOverride {
  company: Company;
  application: StudentApplication;
}

const companyOverrides = new Map<string, CompanyOverride>();

export function resetStudentCompanyMock() {
  companyOverrides.clear();
}

/** Admin korxona qatori → profil `StudentCompany` shakli (rahbar/lokatsiya — mock qiymatlar). */
function toStudentCompany(c: Company): StudentCompany {
  return {
    id: c.id,
    name: c.name,
    tin: c.tin,
    activity: c.activity,
    address: c.address,
    supervisorName: "Mas'ul xodim",
    supervisorPhone: '+998900000000',
    mentorName: null,
    mentorPhone: null,
    lat: 41.3111,
    lng: 69.2797,
    radiusM: c.radiusM,
  };
}

/** Tyutor mock korxonasi id'si → admin korxonalari id'si (nom bo'yicha moslanadi). */
function adminCompanyId(name: string, fallback: string): string {
  return mockCompanies.find((c) => c.name === name)?.id ?? fallback;
}

/**
 * Tanlangan davrdagi korxona (tarix): override faqat ochiq (sukut) davrga tegadi; admin qatorida
 * aktiv korxona yo'q bo'lsa, manba profilning joriy davr korxonasi ham ko'rsatilmaydi (yopilgan
 * davrlardagi tarix saqlanadi).
 */
function resolveCompany(
  row: Student,
  base: StudentCompany | null,
  baseActive: ActiveCompanyRef | null,
  isDefaultPeriod: boolean,
): StudentCompany | null {
  const override = companyOverrides.get(row.id);
  if (override && isDefaultPeriod) return toStudentCompany(override.company);
  if (!base) return null;
  if (row.company === null && base.id === baseActive?.id) return null;
  return { ...base, id: adminCompanyId(base.name, base.id) };
}

/** Aktiv korxona (davrdan mustaqil): override → admin qatori (`company`) bilan mos manba profil. */
function resolveActiveCompany(
  row: Student,
  baseActive: ActiveCompanyRef | null,
  defaultPeriod: StudentPeriodOption | null,
): ActiveCompanyRef | null {
  const override = companyOverrides.get(row.id);
  if (override) {
    return {
      id: override.company.id,
      name: override.company.name,
      periodId: defaultPeriod?.id ?? baseActive?.periodId ?? '',
      periodName: defaultPeriod?.name ?? baseActive?.periodName ?? '',
    };
  }
  if (row.company === null || !baseActive) return null;
  return { ...baseActive, id: adminCompanyId(row.company, baseActive.id), name: row.company };
}

function buildAdminDetail(
  adminId: string,
  periodId: string | null,
): AdminStudentDetail | 'periodNotFound' | null {
  const row = mockStudents.find((s) => s.id === adminId);
  const source = sourceId(adminId);
  const base = source ? buildDetail(source, periodId) : null;
  if (!row || !base) return null;
  if (base === 'periodNotFound') return base;

  const override = companyOverrides.get(row.id);
  const defaultPeriod = base.periods.find((p) => p.isDefault) ?? null;
  const isDefaultPeriod = base.selectedPeriodId === defaultPeriod?.id;
  return {
    ...base,
    company: resolveCompany(row, base.company, base.activeCompany, isDefaultPeriod),
    activeCompany: resolveActiveCompany(row, base.activeCompany, defaultPeriod),
    application: override && isDefaultPeriod ? override.application : base.application,
    id: row.id,
    name: row.fullName,
    hemisId: row.hemisId,
    group: row.group,
    course: row.course,
    faculty: row.faculty,
    groupId: row.groupId,
    department: DEPARTMENTS[row.faculty] ?? 'Umumiy kafedra',
    adminStatus: row.status,
    telegramLinked: row.telegramLinked,
    tutor: TUTORS[row.faculty] ?? null,
    hasPassword: hasMockStudentPassword(row.id),
  };
}

/* ────────────────────────────────────────────────────────────────────────────
 * Filtrlar (`GET /students/filters`, `GET /students?facultyId=&directionId=&course=`).
 * Qiymatlar mock talabalardan hisoblanadi: fakultet — nomi bo'yicha `mockFaculties`dan,
 * yo'nalish — guruh kodi bo'yicha ierarxiya mock'idan (`mockGroups` → `mockDirections`).
 * ──────────────────────────────────────────────────────────────────────────── */

interface StudentScope {
  faculty: { id: string; name: string } | null;
  direction: { id: string; name: string } | null;
}

function studentScope(row: Student): StudentScope {
  const faculty = mockFaculties.find((f) => f.name === row.faculty) ?? null;
  const group = mockGroups.find((g) => g.code === row.group);
  const direction = group ? (mockDirections.find((d) => d.id === group.directionId) ?? null) : null;
  return {
    faculty: faculty && { id: faculty.id, name: faculty.name },
    direction: direction && { id: direction.id, name: direction.name },
  };
}

/** Mock talabalardagi fakultet/yo'nalish/kurslar (takrorlarsiz, nom/raqam bo'yicha tartiblangan). */
export function buildStudentFilters(rows: readonly Student[] = mockStudents): StudentFilters {
  const faculties = new Map<string, string>();
  const directions = new Map<string, { id: string; name: string; facultyId: string }>();
  const courses = new Set<number>();
  for (const row of rows) {
    const { faculty, direction } = studentScope(row);
    courses.add(row.course);
    if (!faculty) continue;
    faculties.set(faculty.id, faculty.name);
    if (direction) directions.set(direction.id, { ...direction, facultyId: faculty.id });
  }
  const byName = (a: { name: string }, b: { name: string }) => a.name.localeCompare(b.name);
  return {
    faculties: [...faculties].map(([id, name]) => ({ id, name })).sort(byName),
    directions: [...directions.values()].sort(byName),
    courses: [...courses].sort((a, b) => a - b),
  };
}

/** `?facultyId=&directionId=&course=` — berilganlari AND bilan qo'llanadi. */
function applyStudentFilters(rows: readonly Student[], url: string): Student[] {
  const sp = new URL(url).searchParams;
  const facultyId = sp.get('facultyId');
  const directionId = sp.get('directionId');
  const course = sp.get('course');
  return rows.filter((row) => {
    const scope = studentScope(row);
    if (facultyId && scope.faculty?.id !== facultyId) return false;
    if (directionId && scope.direction?.id !== directionId) return false;
    if (course && row.course !== Number(course)) return false;
    return true;
  });
}

/** Mock hisobot: bir nechta qator qabul qilinadi, xatolari ro'yxat bo'lib qaytadi (backend shakli). */
export const mockImportResult: ImportResult = {
  totalRows: 5,
  created: 3,
  failed: 2,
  errors: [
    {
      row: 4,
      column: 'HEMIS ID',
      value: '12ab',
      message: "HEMIS ID 5–20 ta raqamdan iborat bo'lishi kerak.",
    },
    {
      row: 6,
      column: 'Guruh',
      value: '999-99',
      message: "Bunday faol guruh yo'q — shablonning «Guruhlar» varag'idan tanlang.",
    },
  ],
};

export const studentsHandlers: HttpHandler[] = [
  // Shablon — haqiqiy .xlsx emas, faqat oqimni tekshirish uchun (blob + fayl nomi).
  http.get(
    STUDENTS_TEMPLATE_ENDPOINT,
    () =>
      new HttpResponse('mock-xlsx', {
        headers: {
          'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
          'Content-Disposition': `attachment; filename="${STUDENTS_TEMPLATE_FILE_NAME}"`,
        },
      }),
  ),

  http.post(STUDENTS_IMPORT_ENDPOINT, async ({ request }) => {
    const form = await request.formData().catch(() => null);
    const file = form?.get('file');
    if (!(file instanceof File) || file.size === 0) {
      return problemResponse(400, "Ma'lumotlar noto'g'ri", 'Excel fayl tanlanmagan.');
    }
    return HttpResponse.json(mockImportResult);
  }),

  // Ommaviy biriktirish: birinchi talaba biriktiriladi, ikkinchisi (bo'lsa) sabab bilan tashlanadi
  // — hisobot oqimini (biriktirildi + rad etilganlar jadvali) tekshirish uchun.
  http.post(STUDENTS_ASSIGN_COMPANY_ENDPOINT, async ({ request }) => {
    const body = (await request.json().catch(() => null)) as AssignCompanyInput | null;
    const ids = body?.studentIds ?? [];
    if (ids.length === 0) {
      return problemResponse(400, "Ma'lumotlar noto'g'ri", 'Kamida bitta talabani belgilang.');
    }
    if (ids.length > 200) {
      return problemResponse(
        400,
        "Ma'lumotlar noto'g'ri",
        "Bir marta 200 tadan ko'p talabani biriktirib bo'lmaydi.",
      );
    }

    const company = mockCompanies.find((c) => c.id === body?.companyId);
    if (!company) return problemResponse(404, 'Topilmadi', 'Korxona topilmadi.');
    if (!company.isActive) {
      return problemResponse(
        409,
        'Amal bajarilmadi',
        'Korxona faol emas — avval uni faollashtiring.',
      );
    }

    const errors: AssignCompanyError[] = ids.slice(1, 2).map((id) => ({
      studentId: id,
      studentName: mockStudents.find((s) => s.id === id)?.fullName ?? id,
      message: 'Allaqachon shu korxonaga biriktirilgan.',
    }));

    return HttpResponse.json({
      total: ids.length,
      assigned: ids.length - errors.length,
      skipped: errors.length,
      companyName: company.name,
      errors,
    });
  }),

  // Profildan bitta talabani biriktirish / o'tkazish. Xatolar: noma'lum talaba/korxona → 404,
  // faol bo'lmagan korxona yoki joriy korxona → 409, korxona tanlanmagan / uzun izoh → 400.
  http.post(`${STUDENTS_ENDPOINT}/:id/company`, async ({ params, request }) => {
    const id = String(params['id']);
    const row = mockStudents.find((s) => s.id === id);
    if (!row) return notFound();

    const body = (await request.json().catch(() => null)) as SetStudentCompanyInput | null;
    if (!body?.companyId) {
      return problemResponse(400, "Ma'lumotlar noto'g'ri", 'Korxonani tanlang.', {
        errors: { CompanyId: ['Korxonani tanlang.'] },
      });
    }
    if ((body.comment ?? '').length > STUDENT_COMPANY_COMMENT_MAX) {
      return problemResponse(400, "Ma'lumotlar noto'g'ri", 'Izoh juda uzun.', {
        errors: { Comment: [`Izoh ${STUDENT_COMPANY_COMMENT_MAX} belgidan oshmasligi kerak.`] },
      });
    }

    const company = mockCompanies.find((c) => c.id === body.companyId);
    if (!company) return problemResponse(404, 'Topilmadi', 'Korxona topilmadi.');
    if (!company.isActive) {
      return problemResponse(409, 'Amal bajarilmadi', 'Korxona faol emas.');
    }

    const current = buildAdminDetail(id, null);
    if (current && current !== 'periodNotFound' && current.activeCompany?.id === company.id) {
      return problemResponse(
        409,
        'Amal bajarilmadi',
        'Talaba allaqachon shu korxonaga biriktirilgan.',
      );
    }

    const now = new Date().toISOString();
    companyOverrides.set(id, {
      company,
      application: {
        id: `app-${id}-${company.id}`,
        status: 'approved',
        submittedAt: now,
        decidedAt: now,
        comment: body.comment?.trim() || null,
        contract: null,
      },
    });
    const detail = buildAdminDetail(id, null);
    return detail && detail !== 'periodNotFound' ? HttpResponse.json(detail) : notFound();
  }),

  // `/:id` dan oldin — aks holda "filters" talaba id'si sifatida tushib qoladi.
  http.get(STUDENTS_FILTERS_ENDPOINT, () => HttpResponse.json(buildStudentFilters())),

  http.get(`${STUDENTS_ENDPOINT}/:id/attendance`, ({ params, request }) => {
    const source = sourceId(String(params['id']));
    if (!source) return notFound();
    const q = new URL(request.url).searchParams;
    return mockAttendanceResponse(source, q.get('periodId'), q.get('from'), q.get('to'));
  }),

  http.get(`${STUDENTS_ENDPOINT}/:id/diaries`, ({ params, request }) => {
    const source = sourceId(String(params['id']));
    if (!source) return notFound();
    return mockDiariesResponse(source, new URL(request.url).searchParams.get('periodId'));
  }),

  http.get(`${STUDENTS_ENDPOINT}/:id`, ({ params, request }) => {
    const periodId = new URL(request.url).searchParams.get('periodId');
    const detail = buildAdminDetail(String(params['id']), periodId);
    if (detail === 'periodNotFound') return periodNotFound();
    return detail ? HttpResponse.json(detail) : notFound();
  }),

  http.get(STUDENTS_ENDPOINT, ({ request }) =>
    HttpResponse.json(
      paginateMock(
        request.url,
        applyStudentFilters(mockStudents, request.url).map((s) => {
          const override = companyOverrides.get(s.id);
          return override ? { ...s, company: override.company.name } : s;
        }),
        (s) => [s.fullName, s.hemisId, s.group, s.faculty],
      ),
    ),
  ),
];

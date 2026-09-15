import { http, HttpResponse, type HttpHandler } from 'msw';
import { mockFaculties } from '../faculties/mocks';
import { problemResponse } from '../shared/mockProblem';
import { paginateMock } from '../shared/paginate';
import { TUTORS_ENDPOINT } from './api';
import type {
  AvailableGroup,
  Tutor,
  TutorCreateInput,
  TutorDetail,
  TutorGroup,
  TutorStatus,
  TutorUpdateInput,
} from './types';

/**
 * Mock ichki holati: tyutor urug'i (`TutorSeed`) + fakultet bo'yicha guruh katalogi + biriktirmalar
 * (`groupId → tutorId`). `TutorRow`/`TutorDetail` shundan hisoblanadi: `groups`/`students` —
 * biriktirmalardan, qolgan ko'rsatkichlar (pending, qaror tezligi, holat) — statik urug'.
 */
interface TutorSeed {
  id: string;
  fullName: string;
  hemisId: string;
  phone: string | null;
  facultyId: string;
  isActive: boolean;
  lastLoginAt: string | null;
  createdAt: string;
  pending: number;
  oldestPendingAt: string | null;
  avgDecisionHours: number | null;
  status: TutorStatus;
}

interface GroupSeed {
  id: string;
  facultyId: string;
  name: string;
  course: number;
  directionName: string;
  departmentName: string;
  students: number;
  isActive: boolean;
}

interface Assignment {
  id: string;
  groupId: string;
  tutorId: string;
}

/** Faol o'quv yili (barcha biriktirmalar shu yilga tegishli). */
const ACADEMIC_YEAR = '2026-2027';

/** Backend `TutorRow` shaklida (SPEC-SCREENS §9.5 raqamlari); `t5` — `t1` bilan bir fakultetda (band guruh sinovi). */
const TUTOR_SEED: TutorSeed[] = [
  {
    id: 't1',
    fullName: 'Nodira Saidova',
    hemisId: '100000000002',
    phone: '+998901112233',
    facultyId: 'f1',
    isActive: true,
    lastLoginAt: '2026-10-12T04:31:00+00:00',
    createdAt: '2026-08-20T09:00:00+00:00',
    pending: 0,
    oldestPendingAt: null,
    avgDecisionHours: 4,
    status: 'active',
  },
  {
    id: 't2',
    fullName: 'Baxtiyor Rasulov',
    hemisId: '100000000003',
    phone: '+998912445102',
    facultyId: 'f2',
    isActive: true,
    lastLoginAt: '2026-10-11T13:10:00+00:00',
    createdAt: '2026-08-20T09:05:00+00:00',
    pending: 7,
    oldestPendingAt: '2026-10-08T04:00:00+00:00',
    avgDecisionHours: 84,
    status: 'late',
  },
  {
    id: 't3',
    fullName: 'Dilshod Ergashev',
    hemisId: '100000000004',
    phone: '+998937001845',
    facultyId: 'f3',
    isActive: true,
    lastLoginAt: '2026-10-12T03:00:00+00:00',
    createdAt: '2026-08-21T10:00:00+00:00',
    pending: 2,
    oldestPendingAt: '2026-10-11T06:30:00+00:00',
    avgDecisionHours: 24,
    status: 'active',
  },
  {
    id: 't4',
    fullName: "Gulnora Yo'ldosheva",
    hemisId: '100000000005',
    phone: '+998975126330',
    facultyId: 'f4',
    isActive: true,
    lastLoginAt: null,
    createdAt: '2026-09-01T08:30:00+00:00',
    pending: 1,
    oldestPendingAt: '2026-10-12T02:15:00+00:00',
    avgDecisionHours: 6,
    status: 'active',
  },
  {
    id: 't5',
    fullName: 'Sardor Karimov',
    hemisId: '100000000006',
    phone: null,
    facultyId: 'f1',
    isActive: true,
    lastLoginAt: '2026-10-10T07:45:00+00:00',
    createdAt: '2026-09-03T11:00:00+00:00',
    pending: 0,
    oldestPendingAt: null,
    avgDecisionHours: null,
    status: 'active',
  },
];

function group(
  facultyId: string,
  name: string,
  course: number,
  departmentName: string,
  directionName: string,
  students: number,
  isActive = true,
): GroupSeed {
  return {
    id: `g-${name}`,
    facultyId,
    name,
    course,
    directionName,
    departmentName,
    students,
    isActive,
  };
}

/** Fakultet bo'yicha guruh katalogi (`available-groups`). `g-441-22` faol emas — ro'yxatga chiqmaydi. */
const GROUP_SEED: GroupSeed[] = [
  group('f1', '412-22', 3, 'Kompyuter injiniringi kafedrasi', 'Kompyuter injiniringi', 19),
  group('f1', '413-22', 3, 'Kompyuter injiniringi kafedrasi', 'Kompyuter injiniringi', 19),
  group('f1', '421-23', 2, 'Kompyuter injiniringi kafedrasi', 'Dasturiy injiniring', 0),
  group('f1', '422-23', 2, 'Kompyuter injiniringi kafedrasi', 'Dasturiy injiniring', 17),
  group('f1', '431-22', 3, 'Axborot xavfsizligi kafedrasi', 'Axborot xavfsizligi', 21),
  group('f1', '441-22', 3, 'Axborot xavfsizligi kafedrasi', 'Axborot xavfsizligi', 0, false),
  group('f2', '221-23', 2, 'Moliya kafedrasi', 'Bank ishi', 24),
  group('f2', '222-23', 2, 'Moliya kafedrasi', 'Bank ishi', 23),
  group('f2', '223-23', 2, 'Moliya kafedrasi', 'Bank ishi', 22),
  group('f2', '231-24', 1, 'Moliya kafedrasi', 'Bank ishi', 26),
  group('f2', '321-22', 3, 'Moliya kafedrasi', 'Moliyaviy menejment', 25),
  group('f2', '322-22', 3, 'Moliya kafedrasi', 'Moliyaviy menejment', 24),
  group('f3', '318-21', 4, 'Arxitektura kafedrasi', 'Arxitektura', 22),
  group('f3', '319-21', 4, 'Arxitektura kafedrasi', 'Arxitektura', 21),
  group('f3', '320-21', 4, 'Arxitektura kafedrasi', 'Arxitektura', 21),
  group('f4', '101-24', 1, "O'zbek tili kafedrasi", "O'zbek filologiyasi", 20),
  group('f4', '102-24', 1, "O'zbek tili kafedrasi", "O'zbek filologiyasi", 21),
  group('f4', '201-23', 2, "O'zbek tili kafedrasi", "O'zbek filologiyasi", 20),
  group('f4', '202-23', 2, "O'zbek tili kafedrasi", "O'zbek filologiyasi", 21),
];

const ASSIGNMENT_SEED: Assignment[] = [
  ['412-22', 't1'],
  ['413-22', 't1'],
  ['221-23', 't2'],
  ['222-23', 't2'],
  ['223-23', 't2'],
  ['321-22', 't2'],
  ['322-22', 't2'],
  ['318-21', 't3'],
  ['319-21', 't3'],
  ['320-21', 't3'],
  ['101-24', 't4'],
  ['102-24', 't4'],
  ['201-23', 't4'],
  ['202-23', 't4'],
  ['431-22', 't5'],
].map(([name, tutorId], i) => ({ id: `a${i + 1}`, groupId: `g-${name}`, tutorId: tutorId! }));

/** Magik guruh id — `PUT /groups` da "Faol o'quv yili yo'q." 409 simulyatsiyasi. */
export const NO_ACADEMIC_YEAR_GROUP_ID = 'NO_ACADEMIC_YEAR';

let tutors: TutorSeed[] = structuredClone(TUTOR_SEED);
let assignments: Assignment[] = structuredClone(ASSIGNMENT_SEED);
let nextTutorId = tutors.length + 1;
let nextAssignmentId = assignments.length + 1;

export function resetTutorsMock() {
  tutors = structuredClone(TUTOR_SEED);
  assignments = structuredClone(ASSIGNMENT_SEED);
  nextTutorId = tutors.length + 1;
  nextAssignmentId = assignments.length + 1;
}

function facultyOf(facultyId: string) {
  return mockFaculties.find((f) => f.id === facultyId);
}

function groupsOf(tutorId: string): { assignment: Assignment; group: GroupSeed }[] {
  return assignments
    .filter((a) => a.tutorId === tutorId)
    .flatMap((assignment) => {
      const g = GROUP_SEED.find((x) => x.id === assignment.groupId);
      return g ? [{ assignment, group: g }] : [];
    })
    .sort((a, b) => a.group.name.localeCompare(b.group.name));
}

function toRow(t: TutorSeed): Tutor {
  const faculty = facultyOf(t.facultyId);
  const groups = groupsOf(t.id);
  return {
    id: t.id,
    fullName: t.fullName,
    phone: t.phone,
    facultyId: t.facultyId,
    facultyCode: faculty?.code ?? null,
    facultyName: faculty?.name ?? null,
    groups: groups.map((g) => g.group.name),
    students: groups.reduce((sum, g) => sum + g.group.students, 0),
    pending: t.pending,
    oldestPendingAt: t.oldestPendingAt,
    avgDecisionHours: t.avgDecisionHours,
    lastActiveAt: t.lastLoginAt,
    isActive: t.isActive,
    status: t.status,
  };
}

function toDetail(t: TutorSeed): TutorDetail {
  const faculty = facultyOf(t.facultyId);
  const groups: TutorGroup[] = groupsOf(t.id).map(({ assignment, group: g }) => ({
    assignmentId: assignment.id,
    groupId: g.id,
    groupName: g.name,
    course: g.course,
    directionName: g.directionName,
    students: g.students,
    academicYearName: ACADEMIC_YEAR,
    isActive: true,
  }));
  return {
    id: t.id,
    fullName: t.fullName,
    hemisId: t.hemisId,
    phone: t.phone,
    facultyId: t.facultyId,
    facultyCode: faculty?.code ?? '',
    facultyName: faculty?.name ?? '',
    isActive: t.isActive,
    lastLoginAt: t.lastLoginAt,
    createdAt: t.createdAt,
    groups,
  };
}

function toAvailable(g: GroupSeed): AvailableGroup {
  const assignment = assignments.find((a) => a.groupId === g.id);
  const tutor = assignment ? tutors.find((t) => t.id === assignment.tutorId) : undefined;
  return {
    id: g.id,
    name: g.name,
    course: g.course,
    directionName: g.directionName,
    departmentName: g.departmentName,
    students: g.students,
    tutorId: tutor?.id ?? null,
    tutorName: tutor?.fullName ?? null,
  };
}

/** Bo'sh satr → null; aks holda E.164 tekshiruvi (xato bo'lsa `errors.Phone`). */
function normalizePhone(raw: unknown, errors: Record<string, string[]>): string | null {
  if (raw === undefined || raw === null) return null;
  const s = String(raw).trim();
  if (s === '') return null;
  if (!/^\+998\d{9}$/.test(s)) errors['Phone'] = ["Telefon +998XXXXXXXXX shaklida bo'lishi kerak."];
  return s;
}

function validateCommon(body: Partial<TutorUpdateInput>, errors: Record<string, string[]>) {
  const fullName = (body.fullName ?? '').trim();
  if (!fullName) errors['FullName'] = ['FISH ni kiriting.'];
  else if (fullName.length < 2 || fullName.length > 150)
    errors['FullName'] = ["FISH 2–150 belgi bo'lishi kerak."];
  if (!body.facultyId) errors['FacultyId'] = ['Fakultetni tanlang.'];
  else if (!facultyOf(body.facultyId)) errors['FacultyId'] = ['Fakultet topilmadi.'];
}

function validationProblem(errors: Record<string, string[]>) {
  return problemResponse(400, "Ma'lumotlar noto'g'ri", "Kiritilgan ma'lumotlarda xatolik bor.", {
    errors,
  });
}

function notFound() {
  return problemResponse(404, 'Topilmadi', 'Tyutor topilmadi.');
}

export const tutorsHandlers: HttpHandler[] = [
  http.get(TUTORS_ENDPOINT, ({ request }) => {
    const facultyId = new URL(request.url).searchParams.get('facultyId');
    const rows = tutors.filter((t) => !facultyId || t.facultyId === facultyId).map(toRow);
    return HttpResponse.json(
      paginateMock(request.url, rows, (t) => [t.fullName, t.phone, t.facultyCode, t.facultyName]),
    );
  }),

  http.get(`${TUTORS_ENDPOINT}/:id`, ({ params }) => {
    const existing = tutors.find((t) => t.id === String(params['id']));
    if (!existing) return notFound();
    return HttpResponse.json(toDetail(existing));
  }),

  http.post(TUTORS_ENDPOINT, async ({ request }) => {
    const body = (await request.json().catch(() => ({}))) as Partial<TutorCreateInput>;
    const errors: Record<string, string[]> = {};
    validateCommon(body, errors);
    const hemisId = (body.hemisId ?? '').trim();
    if (!hemisId) errors['HemisId'] = ['HEMIS ID ni kiriting.'];
    else if (!/^\d{5,20}$/.test(hemisId))
      errors['HemisId'] = ["HEMIS ID 5–20 ta raqamdan iborat bo'lishi kerak."];
    if (!body.password) errors['Password'] = ['Parolni kiriting.'];
    else if (body.password.length < 8)
      errors['Password'] = ["Parol kamida 8 ta belgidan iborat bo'lishi kerak."];
    const phone = normalizePhone(body.phone, errors);
    if (Object.keys(errors).length > 0) return validationProblem(errors);

    if (tutors.some((t) => t.hemisId === hemisId)) {
      return problemResponse(409, 'Ziddiyat', 'Bu HEMIS ID bilan foydalanuvchi mavjud.');
    }
    const tutor: TutorSeed = {
      id: `t${nextTutorId++}`,
      fullName: body.fullName!.trim(),
      hemisId,
      phone,
      facultyId: body.facultyId!,
      isActive: true,
      lastLoginAt: null,
      createdAt: new Date().toISOString(),
      pending: 0,
      oldestPendingAt: null,
      avgDecisionHours: null,
      status: 'active',
    };
    tutors = [...tutors, tutor];
    return HttpResponse.json(toDetail(tutor), { status: 201 });
  }),

  http.put(`${TUTORS_ENDPOINT}/:id`, async ({ request, params }) => {
    const id = String(params['id']);
    const existing = tutors.find((t) => t.id === id);
    if (!existing) return notFound();

    const body = (await request.json().catch(() => ({}))) as Partial<TutorUpdateInput>;
    const errors: Record<string, string[]> = {};
    validateCommon(body, errors);
    const phone = normalizePhone(body.phone, errors);
    if (Object.keys(errors).length > 0) return validationProblem(errors);

    if (body.facultyId !== existing.facultyId && assignments.some((a) => a.tutorId === id)) {
      return problemResponse(
        409,
        'Ziddiyat',
        'Tyutorga guruhlar biriktirilgan — avval ularni ajrating.',
      );
    }
    const updated: TutorSeed = {
      ...existing,
      fullName: body.fullName!.trim(),
      phone,
      facultyId: body.facultyId!,
    };
    tutors = tutors.map((t) => (t.id === id ? updated : t));
    return HttpResponse.json(toDetail(updated));
  }),

  http.patch(`${TUTORS_ENDPOINT}/:id/status`, async ({ request, params }) => {
    const id = String(params['id']);
    const existing = tutors.find((t) => t.id === id);
    if (!existing) return notFound();
    const body = (await request.json().catch(() => ({}))) as { isActive?: boolean };
    tutors = tutors.map((t) => (t.id === id ? { ...t, isActive: Boolean(body.isActive) } : t));
    return new HttpResponse(null, { status: 204 });
  }),

  http.post(`${TUTORS_ENDPOINT}/:id/password`, async ({ request, params }) => {
    const existing = tutors.find((t) => t.id === String(params['id']));
    if (!existing) return notFound();
    const body = (await request.json().catch(() => ({}))) as { password?: string };
    if (!body.password || body.password.length < 8) {
      return validationProblem({
        Password: ["Parol kamida 8 ta belgidan iborat bo'lishi kerak."],
      });
    }
    return new HttpResponse(null, { status: 204 });
  }),

  http.get(`${TUTORS_ENDPOINT}/:id/available-groups`, ({ params }) => {
    const existing = tutors.find((t) => t.id === String(params['id']));
    if (!existing) return notFound();
    const rows = GROUP_SEED.filter((g) => g.facultyId === existing.facultyId && g.isActive).map(
      toAvailable,
    );
    return HttpResponse.json(rows);
  }),

  http.put(`${TUTORS_ENDPOINT}/:id/groups`, async ({ request, params }) => {
    const id = String(params['id']);
    const existing = tutors.find((t) => t.id === id);
    if (!existing) return notFound();
    const body = (await request.json().catch(() => ({}))) as { groupIds?: unknown };
    const groupIds = Array.isArray(body.groupIds) ? body.groupIds.map(String) : null;
    if (!groupIds) return validationProblem({ GroupIds: ["Guruhlar ro'yxati kerak."] });

    if (groupIds.includes(NO_ACADEMIC_YEAR_GROUP_ID)) {
      return problemResponse(409, 'Ziddiyat', "Faol o'quv yili yo'q.");
    }
    const unique = [...new Set(groupIds)];
    for (const groupId of unique) {
      const g = GROUP_SEED.find((x) => x.id === groupId);
      if (!g || !g.isActive || g.facultyId !== existing.facultyId) {
        return validationProblem({
          GroupIds: [`'${groupId}' guruhi tyutor fakultetida topilmadi yoki faol emas.`],
        });
      }
      const taken = assignments.find((a) => a.groupId === groupId && a.tutorId !== id);
      if (taken) {
        const other = tutors.find((t) => t.id === taken.tutorId);
        return problemResponse(
          409,
          'Ziddiyat',
          `${g.name} guruhi ${other?.fullName ?? taken.tutorId} tyutoriga biriktirilgan.`,
        );
      }
    }
    // To'plamni almashtirish: mavjudlarini saqlab (assignmentId o'zgarmaydi), yangilarini qo'shish.
    const kept = assignments.filter((a) => a.tutorId !== id || unique.includes(a.groupId));
    const added = unique
      .filter((groupId) => !kept.some((a) => a.tutorId === id && a.groupId === groupId))
      .map((groupId) => ({ id: `a${nextAssignmentId++}`, groupId, tutorId: id }));
    assignments = [...kept, ...added];
    return HttpResponse.json(toDetail(existing));
  }),
];

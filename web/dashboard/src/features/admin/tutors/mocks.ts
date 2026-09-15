import { http, HttpResponse, type HttpHandler } from 'msw';
import { mockFaculties } from '../faculties/mocks';
import { problemResponse } from '../shared/mockProblem';
import { paginateMock } from '../shared/paginate';
import { TUTORS_ENDPOINT } from './api';
import {
  SCOPE_LEVEL_LABEL,
  type FacultyRef,
  type ScopeTreeDepartment,
  type ScopeTreeDirection,
  type ScopeTreeGroup,
  type Tutor,
  type TutorCreateInput,
  type TutorDetail,
  type TutorGroup,
  type TutorScope,
  type TutorScopeInput,
  type TutorScopeLevel,
  type TutorScopeTree,
  type TutorStatus,
  type TutorUpdateInput,
} from './types';

/**
 * Mock ichki holati: tyutor urug'i (`TutorSeed`) + fakultet ierarxiyasi katalogi (kafedra → yo'nalish
 * → guruh) + ko'lamlar (`ScopeSeed`: tyutor → tugun). `TutorRow`/`TutorDetail` shundan hisoblanadi:
 * `scopes` — ko'lamlardan, `groups`/`students` — ko'lamlarni daraxt bo'ylab yoyib; qolgan
 * ko'rsatkichlar (pending, qaror tezligi, holat) — statik urug'.
 */
interface TutorSeed {
  id: string;
  fullName: string;
  hemisId: string;
  phone: string | null;
  /** Bir nechta fakultet bo'lishi mumkin (javobda nom tartibida). */
  facultyIds: string[];
  isActive: boolean;
  lastLoginAt: string | null;
  createdAt: string;
  pending: number;
  oldestPendingAt: string | null;
  avgDecisionHours: number | null;
  status: TutorStatus;
}

interface DepartmentSeed {
  id: string;
  facultyId: string;
  name: string;
}

interface DirectionSeed {
  id: string;
  departmentId: string;
  name: string;
}

interface GroupSeed {
  id: string;
  directionId: string;
  name: string;
  course: number;
  students: number;
  isActive: boolean;
}

/** Ko'lam yozuvi: `tutorId` tyutoriga `level` darajasidagi `nodeId` tuguni biriktirilgan. */
interface ScopeSeed {
  id: string;
  tutorId: string;
  level: TutorScopeLevel;
  nodeId: string;
}

/** Faol o'quv yili (barcha biriktirmalar shu yilga tegishli). */
const ACADEMIC_YEAR = '2026-2027';

/**
 * Backend `TutorRow` shaklida (SPEC-SCREENS §9.5 raqamlari); `t5` — `t1` bilan bir fakultetda (band tugun
 * sinovi). `t1` — ikki fakultetli (f1 + f2): ro'yxatda "AT, IM", ko'lam tanlashda ikki daraxt.
 */
const TUTOR_SEED: TutorSeed[] = [
  {
    id: 't1',
    fullName: 'Nodira Saidova',
    hemisId: '100000000002',
    phone: '+998901112233',
    facultyIds: ['f1', 'f2'],
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
    facultyIds: ['f2'],
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
    facultyIds: ['f3'],
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
    facultyIds: ['f4'],
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
    facultyIds: ['f1'],
    isActive: true,
    lastLoginAt: '2026-10-10T07:45:00+00:00',
    createdAt: '2026-09-03T11:00:00+00:00',
    pending: 0,
    oldestPendingAt: null,
    avgDecisionHours: null,
    status: 'active',
  },
];

/**
 * Ierarxiya katalogi (`scope-tree`). f1 — 3 kafedra: `d1` (2 yo'nalish), `d2` (1 yo'nalish, `g-441-22`
 * faol emas — daraxtga chiqmaydi), `d5` (Sardor Karimovga to'liq biriktirilgan — kafedra darajasidagi band tugun).
 * f2 — 2 kafedra: `d3` (Baxtiyor Rasulovniki), `d8` (erkin — t1 ikkinchi fakultetida tanlashi mumkin).
 */
const DEPARTMENT_SEED: DepartmentSeed[] = [
  { id: 'd1', facultyId: 'f1', name: 'Kompyuter injiniringi kafedrasi' },
  { id: 'd2', facultyId: 'f1', name: 'Axborot xavfsizligi kafedrasi' },
  { id: 'd5', facultyId: 'f1', name: "Sun'iy intellekt kafedrasi" },
  { id: 'd3', facultyId: 'f2', name: 'Moliya kafedrasi' },
  { id: 'd8', facultyId: 'f2', name: 'Buxgalteriya hisobi kafedrasi' },
  { id: 'd6', facultyId: 'f3', name: 'Arxitektura kafedrasi' },
  { id: 'd7', facultyId: 'f4', name: "O'zbek tili kafedrasi" },
];

const DIRECTION_SEED: DirectionSeed[] = [
  { id: 'dir1', departmentId: 'd1', name: 'Kompyuter injiniringi' },
  { id: 'dir2', departmentId: 'd1', name: 'Dasturiy injiniring' },
  { id: 'dir5', departmentId: 'd2', name: 'Axborot xavfsizligi' },
  { id: 'dir6', departmentId: 'd5', name: "Sun'iy intellekt" },
  { id: 'dir3', departmentId: 'd3', name: 'Bank ishi' },
  { id: 'dir4', departmentId: 'd3', name: 'Moliyaviy menejment' },
  { id: 'dir9', departmentId: 'd8', name: 'Buxgalteriya hisobi' },
  { id: 'dir7', departmentId: 'd6', name: 'Arxitektura' },
  { id: 'dir8', departmentId: 'd7', name: "O'zbek filologiyasi" },
];

function group(
  directionId: string,
  name: string,
  course: number,
  students: number,
  isActive = true,
): GroupSeed {
  return { id: `g-${name}`, directionId, name, course, students, isActive };
}

const GROUP_SEED: GroupSeed[] = [
  group('dir1', '412-22', 3, 19),
  group('dir1', '413-22', 3, 19),
  group('dir2', '421-23', 2, 0),
  group('dir2', '422-23', 2, 17),
  group('dir5', '431-22', 3, 21),
  group('dir5', '432-22', 3, 18),
  group('dir5', '441-22', 3, 0, false),
  group('dir6', '451-23', 2, 18),
  group('dir6', '452-23', 2, 16),
  group('dir3', '221-23', 2, 24),
  group('dir3', '222-23', 2, 23),
  group('dir3', '223-23', 2, 22),
  group('dir4', '321-22', 3, 25),
  group('dir4', '322-22', 3, 24),
  group('dir9', '231-23', 2, 20),
  group('dir9', '232-23', 2, 19),
  group('dir7', '318-21', 4, 22),
  group('dir7', '319-21', 4, 21),
  group('dir7', '320-21', 4, 21),
  group('dir8', '101-24', 1, 20),
  group('dir8', '102-24', 1, 21),
  group('dir8', '201-23', 2, 20),
  group('dir8', '202-23', 2, 21),
];

/**
 * Ko'lam urug'i: t1 — `dir1` yo'nalishi (412-22, 413-22); t2 — `d3` kafedrasi (f2 dagi 5 guruh);
 * t3 — `d6` kafedrasi; t4 — butun `f4`; t5 — `d5` kafedrasi (band kafedra) + `g-431-22` guruhi (band guruh).
 */
const SCOPE_SEED: ScopeSeed[] = [
  { id: 's1', tutorId: 't1', level: 'direction', nodeId: 'dir1' },
  { id: 's2', tutorId: 't2', level: 'department', nodeId: 'd3' },
  { id: 's3', tutorId: 't3', level: 'department', nodeId: 'd6' },
  { id: 's4', tutorId: 't4', level: 'faculty', nodeId: 'f4' },
  { id: 's5', tutorId: 't5', level: 'department', nodeId: 'd5' },
  { id: 's6', tutorId: 't5', level: 'group', nodeId: 'g-431-22' },
];

/** Magik tugun id — `PUT /scopes` da "Faol o'quv yili yo'q." 409 simulyatsiyasi. */
export const NO_ACADEMIC_YEAR_SCOPE_ID = 'NO_ACADEMIC_YEAR';

let tutors: TutorSeed[] = structuredClone(TUTOR_SEED);
let scopes: ScopeSeed[] = structuredClone(SCOPE_SEED);
let nextTutorId = tutors.length + 1;
let nextScopeId = scopes.length + 1;

export function resetTutorsMock() {
  tutors = structuredClone(TUTOR_SEED);
  scopes = structuredClone(SCOPE_SEED);
  nextTutorId = tutors.length + 1;
  nextScopeId = scopes.length + 1;
}

function facultyOf(facultyId: string) {
  return mockFaculties.find((f) => f.id === facultyId);
}

/** Tyutor fakultetlari — `FacultyRef[]`, nom tartibida (kontrakt). */
function facultiesOf(facultyIds: readonly string[]): FacultyRef[] {
  return facultyIds
    .map(facultyOf)
    .filter((f): f is NonNullable<typeof f> => f !== undefined)
    .map((f) => ({ id: f.id, code: f.code, name: f.name }))
    .sort((a, b) => a.name.localeCompare(b.name));
}

/** Tugunning ajdodlar zanjiri (fakultetgacha) — `level:id` kalitlar to'plami. */
interface NodeRef {
  level: TutorScopeLevel;
  id: string;
}

const LEVEL_ORDER: TutorScopeLevel[] = ['faculty', 'department', 'direction', 'group'];

function nodeKey(n: NodeRef) {
  return `${n.level}:${n.id}`;
}

/** Tugun va uning ajdodlari (o'zi ham kiradi), fakultetdan boshlab. Tugun topilmasa — null. */
function lineage(n: NodeRef): NodeRef[] | null {
  if (n.level === 'faculty') return facultyOf(n.id) ? [n] : null;
  if (n.level === 'department') {
    const d = DEPARTMENT_SEED.find((x) => x.id === n.id);
    return d ? [{ level: 'faculty', id: d.facultyId }, n] : null;
  }
  if (n.level === 'direction') {
    const dir = DIRECTION_SEED.find((x) => x.id === n.id);
    const up = dir ? lineage({ level: 'department', id: dir.departmentId }) : null;
    return up ? [...up, n] : null;
  }
  const g = GROUP_SEED.find((x) => x.id === n.id);
  const up = g ? lineage({ level: 'direction', id: g.directionId }) : null;
  return up ? [...up, n] : null;
}

/** Tugun ostidagi faol guruhlar (guruh darajasida — o'zi, faol bo'lsa). */
function groupsUnder(n: NodeRef): GroupSeed[] {
  const active = GROUP_SEED.filter((g) => g.isActive);
  switch (n.level) {
    case 'group':
      return active.filter((g) => g.id === n.id);
    case 'direction':
      return active.filter((g) => g.directionId === n.id);
    case 'department': {
      const dirs = new Set(DIRECTION_SEED.filter((d) => d.departmentId === n.id).map((d) => d.id));
      return active.filter((g) => dirs.has(g.directionId));
    }
    case 'faculty': {
      const deps = new Set(DEPARTMENT_SEED.filter((d) => d.facultyId === n.id).map((d) => d.id));
      const dirs = new Set(DIRECTION_SEED.filter((d) => deps.has(d.departmentId)).map((d) => d.id));
      return active.filter((g) => dirs.has(g.directionId));
    }
  }
}

function nodeName(n: NodeRef): string {
  switch (n.level) {
    case 'faculty':
      return facultyOf(n.id)?.name ?? '';
    case 'department':
      return DEPARTMENT_SEED.find((x) => x.id === n.id)?.name ?? '';
    case 'direction':
      return DIRECTION_SEED.find((x) => x.id === n.id)?.name ?? '';
    case 'group':
      return GROUP_SEED.find((x) => x.id === n.id)?.name ?? '';
  }
}

function toScope(s: ScopeSeed): TutorScope {
  const n: NodeRef = { level: s.level, id: s.nodeId };
  const chain = lineage(n) ?? [n];
  const byLevel = (level: TutorScopeLevel) => chain.find((x) => x.level === level)?.id ?? null;
  const groups = groupsUnder(n);
  return {
    id: s.id,
    level: s.level,
    facultyId: byLevel('faculty') ?? '',
    departmentId: byLevel('department'),
    directionId: byLevel('direction'),
    groupId: byLevel('group'),
    name: nodeName(n),
    path: chain.slice(0, -1).map(nodeName).join(' › '),
    groups: groups.length,
    students: groups.reduce((sum, g) => sum + g.students, 0),
  };
}

function scopesOf(tutorId: string): ScopeSeed[] {
  return scopes
    .filter((s) => s.tutorId === tutorId)
    .sort(
      (a, b) =>
        LEVEL_ORDER.indexOf(a.level) - LEVEL_ORDER.indexOf(b.level) ||
        nodeName({ level: a.level, id: a.nodeId }).localeCompare(
          nodeName({ level: b.level, id: b.nodeId }),
        ),
    );
}

/** Ko'lamlarni daraxt bo'ylab yoyib — samarali guruhlar (nom bo'yicha tartiblangan). */
function groupsOf(tutorId: string): { scope: ScopeSeed; group: GroupSeed }[] {
  return scopesOf(tutorId)
    .flatMap((scope) =>
      groupsUnder({ level: scope.level, id: scope.nodeId }).map((g) => ({ scope, group: g })),
    )
    .sort((a, b) => a.group.name.localeCompare(b.group.name));
}

function toRow(t: TutorSeed): Tutor {
  const groups = groupsOf(t.id);
  return {
    id: t.id,
    fullName: t.fullName,
    phone: t.phone,
    faculties: facultiesOf(t.facultyIds),
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
  const groups: TutorGroup[] = groupsOf(t.id).map(({ scope, group: g }) => ({
    assignmentId: `${scope.id}:${g.id}`,
    groupId: g.id,
    groupName: g.name,
    course: g.course,
    directionName: DIRECTION_SEED.find((d) => d.id === g.directionId)?.name ?? '',
    students: g.students,
    academicYearName: ACADEMIC_YEAR,
    isActive: true,
  }));
  return {
    id: t.id,
    fullName: t.fullName,
    hemisId: t.hemisId,
    phone: t.phone,
    faculties: facultiesOf(t.facultyIds),
    isActive: t.isActive,
    lastLoginAt: t.lastLoginAt,
    createdAt: t.createdAt,
    scopes: scopesOf(t.id).map(toScope),
    groups,
  };
}

/** AYNAN shu tugunda ko'lami bor tyutor. */
function ownerOf(n: NodeRef): { tutorId: string | null; tutorName: string | null } {
  const s = scopes.find((x) => x.level === n.level && x.nodeId === n.id);
  const tutor = s ? tutors.find((t) => t.id === s.tutorId) : undefined;
  return { tutorId: tutor?.id ?? null, tutorName: tutor?.fullName ?? null };
}

function toScopeTree(facultyId: string): TutorScopeTree | null {
  const faculty = facultyOf(facultyId);
  if (!faculty) return null;
  const departments: ScopeTreeDepartment[] = DEPARTMENT_SEED.filter(
    (d) => d.facultyId === facultyId,
  ).map((d) => {
    const directions: ScopeTreeDirection[] = DIRECTION_SEED.filter(
      (dir) => dir.departmentId === d.id,
    ).map((dir) => {
      const groups: ScopeTreeGroup[] = GROUP_SEED.filter(
        (g) => g.directionId === dir.id && g.isActive,
      ).map((g) => ({
        id: g.id,
        name: g.name,
        course: g.course,
        students: g.students,
        ...ownerOf({ level: 'group', id: g.id }),
      }));
      return { id: dir.id, name: dir.name, groups, ...ownerOf({ level: 'direction', id: dir.id }) };
    });
    return { id: d.id, name: d.name, directions, ...ownerOf({ level: 'department', id: d.id }) };
  });
  return {
    id: faculty.id,
    name: faculty.name,
    code: faculty.code,
    departments,
    ...ownerOf({ level: 'faculty', id: faculty.id }),
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
  if (!Array.isArray(body.facultyIds) || body.facultyIds.length === 0)
    errors['FacultyIds'] = ['Kamida bitta fakultet tanlang.'];
}

/**
 * `facultyIds` (takrorlar olib tashlanadi): 404 — topilmasa, 409 — faol emas. Muvaffaqiyatda noyob id'lar.
 * `validateCommon` dan keyin chaqiriladi (bo'sh ro'yxat u yerda 400).
 */
function resolveFacultyIds(raw: readonly string[]): string[] | Response {
  const ids = [...new Set(raw.map(String))];
  for (const id of ids) {
    const faculty = facultyOf(id);
    if (!faculty) return problemResponse(404, 'Topilmadi', 'Fakultet topilmadi.');
    if (!faculty.isActive) {
      return problemResponse(409, 'Ziddiyat', `Fakultet faol emas: ${faculty.name}`);
    }
  }
  return ids;
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
    // Filtr: tyutor fakultetlaridan biri mos kelsa.
    const rows = tutors.filter((t) => !facultyId || t.facultyIds.includes(facultyId)).map(toRow);
    return HttpResponse.json(
      paginateMock(request.url, rows, (t) => [
        t.fullName,
        t.phone,
        ...t.faculties.flatMap((f) => [f.code, f.name]),
      ]),
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

    const facultyIds = resolveFacultyIds(body.facultyIds!);
    if (facultyIds instanceof Response) return facultyIds;
    if (tutors.some((t) => t.hemisId === hemisId)) {
      return problemResponse(409, 'Ziddiyat', 'Bu HEMIS ID bilan foydalanuvchi mavjud.');
    }
    const tutor: TutorSeed = {
      id: `t${nextTutorId++}`,
      fullName: body.fullName!.trim(),
      hemisId,
      phone,
      facultyIds,
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

    const facultyIds = resolveFacultyIds(body.facultyIds!);
    if (facultyIds instanceof Response) return facultyIds;

    // Olib tashlanayotgan fakultetda tyutor ko'lami bo'lsa — 409 (SCOPE_SEED asosida).
    const removed = existing.facultyIds.filter((f) => !facultyIds.includes(f));
    for (const facultyId of removed) {
      const hasScope = scopes.some(
        (s) => s.tutorId === id && lineage({ level: s.level, id: s.nodeId })?.[0]?.id === facultyId,
      );
      if (hasScope) {
        return problemResponse(
          409,
          'Ziddiyat',
          `${facultyOf(facultyId)?.name ?? facultyId} fakultetida tyutorga ko'lam biriktirilgan — avval uni ajrating.`,
        );
      }
    }
    const updated: TutorSeed = {
      ...existing,
      fullName: body.fullName!.trim(),
      phone,
      facultyIds,
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

  http.get(`${TUTORS_ENDPOINT}/:id/scope-tree`, ({ params }) => {
    const existing = tutors.find((t) => t.id === String(params['id']));
    if (!existing) return notFound();
    // Har fakultet uchun bitta daraxt, nom tartibida.
    const trees = existing.facultyIds
      .map(toScopeTree)
      .filter((t): t is TutorScopeTree => t !== null)
      .sort((a, b) => a.name.localeCompare(b.name));
    return HttpResponse.json(trees);
  }),

  http.put(`${TUTORS_ENDPOINT}/:id/scopes`, async ({ request, params }) => {
    const id = String(params['id']);
    const existing = tutors.find((t) => t.id === id);
    if (!existing) return notFound();
    const body = (await request.json().catch(() => ({}))) as { scopes?: unknown };
    if (!Array.isArray(body.scopes)) {
      return validationProblem({ Scopes: ["Ko'lamlar ro'yxati kerak."] });
    }
    const requested: TutorScopeInput[] = [];
    for (const raw of body.scopes as Partial<TutorScopeInput>[]) {
      const level = raw?.level;
      const nodeId = raw?.id === undefined ? '' : String(raw.id);
      if (!level || !LEVEL_ORDER.includes(level) || !nodeId) {
        return validationProblem({ Scopes: ["Ko'lam darajasi yoki id noto'g'ri."] });
      }
      if (nodeId === NO_ACADEMIC_YEAR_SCOPE_ID) {
        return problemResponse(409, 'Ziddiyat', "Faol o'quv yili yo'q.");
      }
      if (!requested.some((r) => r.level === level && r.id === nodeId)) {
        requested.push({ level, id: nodeId });
      }
    }

    // Har bir tugun tyutor fakultetlaridan birida bo'lishi kerak.
    const chains = new Map<string, NodeRef[]>();
    for (const r of requested) {
      const chain = lineage(r);
      const inactive = r.level === 'group' && !GROUP_SEED.find((g) => g.id === r.id)?.isActive;
      if (!chain || !existing.facultyIds.includes(chain[0]!.id) || inactive) {
        return problemResponse(
          400,
          "Ma'lumotlar noto'g'ri",
          `'${r.id}' (${SCOPE_LEVEL_LABEL[r.level].toLowerCase()}) tyutor fakultetida topilmadi yoki faol emas.`,
        );
      }
      chains.set(nodeKey(r), chain);
    }

    // Boshqa tyutor ko'lami bilan kesishuv: teng / ajdod / avlod.
    const others = scopes.filter((s) => s.tutorId !== id);
    for (const r of requested) {
      const chain = chains.get(nodeKey(r))!;
      const chainKeys = new Set(chain.map(nodeKey));
      for (const o of others) {
        const on: NodeRef = { level: o.level, id: o.nodeId };
        const oChain = lineage(on) ?? [on];
        const overlaps =
          chainKeys.has(nodeKey(on)) || oChain.some((x) => nodeKey(x) === nodeKey(r));
        if (overlaps) {
          const owner = tutors.find((t) => t.id === o.tutorId);
          return problemResponse(
            409,
            'Ziddiyat',
            `${nodeName(on)} (${SCOPE_LEVEL_LABEL[o.level].toLowerCase()}) ${owner?.fullName ?? o.tutorId} tyutoriga biriktirilgan.`,
          );
        }
      }
    }

    // Normalizatsiya: boshqa so'ralgan tugunning avlodi bo'lganlar tashlab yuboriladi.
    const requestedKeys = new Set(requested.map(nodeKey));
    const normalized = requested.filter((r) => {
      const chain = chains.get(nodeKey(r))!;
      return !chain.slice(0, -1).some((x) => requestedKeys.has(nodeKey(x)));
    });

    // To'plamni almashtirish: mavjudlarini saqlab (id o'zgarmaydi), yangilarini qo'shish.
    const kept = scopes.filter(
      (s) => s.tutorId !== id || normalized.some((r) => r.level === s.level && r.id === s.nodeId),
    );
    const added: ScopeSeed[] = normalized
      .filter(
        (r) => !kept.some((s) => s.tutorId === id && s.level === r.level && s.nodeId === r.id),
      )
      .map((r) => ({ id: `s${nextScopeId++}`, tutorId: id, level: r.level, nodeId: r.id }));
    scopes = [...kept, ...added];
    return HttpResponse.json(toDetail(existing));
  }),
];

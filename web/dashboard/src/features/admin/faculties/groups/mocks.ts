import { http, HttpResponse, type HttpHandler } from 'msw';
import { mockDirections } from '../directions/mocks';
import { mockFaculties } from '../mocks';
import { problemResponse } from '../../shared/mockProblem';
import { paginateMock } from '../../shared/paginate';
import type { GroupDto, GroupInput, GroupPeriod, GroupRow } from './types';

interface GroupSeed extends GroupRow {
  directionId: string;
}

const PERIOD: GroupPeriod = {
  id: 'p1',
  name: 'Ishlab chiqarish amaliyoti 2026',
  status: 'active',
  startDate: '2026-08-31',
  endDate: '2026-10-14',
};

/**
 * Ierarxiya urug'i: `dir1` ostida 2 to'liq guruh (talabalari bor → o'chirish 409);
 * `dir2` ostida talabasiz/tyutorsiz `g3` (o'chirish 204 sinovi); `dir3` ostida `g4` (talabalari bor).
 */
export const mockGroups: GroupSeed[] = [
  {
    id: 'g1',
    directionId: 'dir1',
    code: '412-22',
    course: 3,
    direction: '',
    faculty: '',
    facultyCode: '',
    tutorId: 't1',
    tutor: 'Nodira Saidova',
    students: 19,
    attendancePct: 91,
    period: PERIOD,
    isActive: true,
  },
  {
    id: 'g2',
    directionId: 'dir1',
    code: '413-22',
    course: 3,
    direction: '',
    faculty: '',
    facultyCode: '',
    tutorId: 't1',
    tutor: 'Nodira Saidova',
    students: 19,
    attendancePct: 88,
    period: PERIOD,
    isActive: true,
  },
  {
    id: 'g3',
    directionId: 'dir2',
    code: '421-23',
    course: 2,
    direction: '',
    faculty: '',
    facultyCode: '',
    tutorId: null,
    tutor: null,
    students: 0,
    attendancePct: 0,
    period: null,
    isActive: true,
  },
  {
    id: 'g4',
    directionId: 'dir3',
    code: '221-23',
    course: 2,
    direction: '',
    faculty: '',
    facultyCode: '',
    tutorId: 't2',
    tutor: 'Baxtiyor Rasulov',
    students: 24,
    attendancePct: 79,
    period: PERIOD,
    isActive: true,
  },
];

/** Faol o'quv yili — hozircha barcha guruh yaratish so'rovlarida mavjud deb hisoblanadi. */
const ACADEMIC_YEAR = '2026-2027';

/** O'chirishga qarshilik ko'rsatadigan guruhlar (talabalari bor). */
const GROUPS_WITH_DEPENDENTS = new Set(['g1', 'g2', 'g4']);

let state: GroupSeed[] = structuredClone(mockGroups);
let nextId = state.length + 1;

export function resetGroupsMock() {
  state = structuredClone(mockGroups);
  nextId = state.length + 1;
}

function directionInfo(directionId: string) {
  const direction = mockDirections.find((d) => d.id === directionId);
  const faculty = mockFaculties.find((f) => f.id === directionFacultyId(directionId));
  return { directionName: direction?.name ?? '', faculty };
}

/** `mockDirections`da `departmentId` bor, `department→faculty` bog'lanishi `departments/mocks`da —
 * dublikat importdan qochish uchun bu yerda shu ikkitagina fakultetni bilamiz (urug' ma'lumotlar bo'yicha). */
function directionFacultyId(directionId: string): string {
  if (directionId === 'dir1' || directionId === 'dir2') return 'f1';
  if (directionId === 'dir3' || directionId === 'dir4') return 'f2';
  return '';
}

function enrich(g: GroupSeed): GroupSeed {
  const { directionName, faculty } = directionInfo(g.directionId);
  return {
    ...g,
    direction: directionName,
    faculty: faculty?.name ?? '',
    facultyCode: faculty?.code ?? '',
  };
}

function toRow(g: GroupSeed): GroupRow {
  const { directionId: _directionId, ...row } = enrich(g);
  return row;
}

function toDto(g: GroupSeed): GroupDto {
  return {
    id: g.id,
    directionId: g.directionId,
    name: g.code,
    course: g.course,
    isActive: g.isActive,
    academicYear: ACADEMIC_YEAR,
  };
}

function validateInput(body: Partial<GroupInput>): Record<string, string[]> {
  const errors: Record<string, string[]> = {};
  const name = (body.name ?? '').trim();
  const course = body.course;
  if (!name) errors['Name'] = ['Guruh nomini kiriting.'];
  else if (!/^[A-Za-z0-9-]{2,20}$/.test(name))
    errors['Name'] = ["Guruh nomi 2–20 ta lotin harf, raqam yoki '-' dan iborat bo'lishi kerak."];
  if (
    course === undefined ||
    course === null ||
    !Number.isInteger(course) ||
    course < 1 ||
    course > 6
  )
    errors['Course'] = ["Kurs 1–6 oralig'ida bo'lishi kerak."];
  return errors;
}

export const groupsHandlers: HttpHandler[] = [
  http.get('/api/admin/directions/:directionId/groups', ({ request, params }) => {
    const directionId = String(params['directionId']);
    const rows = state.filter((g) => g.directionId === directionId).map(toRow);
    return HttpResponse.json(paginateMock(request.url, rows, (g) => [g.code, g.tutor]));
  }),

  http.post('/api/admin/directions/:directionId/groups', async ({ request, params }) => {
    const directionId = String(params['directionId']);
    if (!mockDirections.some((d) => d.id === directionId)) {
      return problemResponse(404, 'Topilmadi', "Yo'nalish topilmadi.");
    }
    const body = (await request.json().catch(() => ({}))) as Partial<GroupInput>;
    const errors = validateInput(body);
    if (Object.keys(errors).length > 0) {
      return problemResponse(
        400,
        "Ma'lumotlar noto'g'ri",
        "Kiritilgan ma'lumotlarda xatolik bor.",
        {
          errors,
        },
      );
    }
    const name = body.name!.trim();
    if (
      state.some(
        (g) => g.directionId === directionId && g.code.toLowerCase() === name.toLowerCase(),
      )
    ) {
      return problemResponse(409, 'Ziddiyat', `'${name}' guruhi bu yo'nalishda allaqachon mavjud.`);
    }
    const group: GroupSeed = {
      id: `g${nextId++}`,
      directionId,
      code: name,
      course: body.course!,
      direction: '',
      faculty: '',
      facultyCode: '',
      tutorId: null,
      tutor: null,
      students: 0,
      attendancePct: 0,
      period: null,
      isActive: true,
    };
    state = [...state, group];
    return HttpResponse.json(toDto(group), { status: 201 });
  }),

  http.get('/api/admin/groups/:id', ({ params }) => {
    const id = String(params['id']);
    const existing = state.find((g) => g.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', 'Guruh topilmadi.');
    return HttpResponse.json(toDto(existing));
  }),

  http.put('/api/admin/groups/:id', async ({ request, params }) => {
    const id = String(params['id']);
    const existing = state.find((g) => g.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', 'Guruh topilmadi.');

    const body = (await request.json().catch(() => ({}))) as Partial<GroupInput>;
    const errors = validateInput(body);
    if (Object.keys(errors).length > 0) {
      return problemResponse(
        400,
        "Ma'lumotlar noto'g'ri",
        "Kiritilgan ma'lumotlarda xatolik bor.",
        {
          errors,
        },
      );
    }
    const name = body.name!.trim();
    if (
      state.some(
        (g) =>
          g.id !== id &&
          g.directionId === existing.directionId &&
          g.code.toLowerCase() === name.toLowerCase(),
      )
    ) {
      return problemResponse(409, 'Ziddiyat', `'${name}' guruhi bu yo'nalishda allaqachon mavjud.`);
    }
    const updated: GroupSeed = { ...existing, code: name, course: body.course! };
    state = state.map((g) => (g.id === id ? updated : g));
    return HttpResponse.json(toDto(updated));
  }),

  http.patch('/api/admin/groups/:id/status', async ({ request, params }) => {
    const id = String(params['id']);
    const existing = state.find((g) => g.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', 'Guruh topilmadi.');
    const body = (await request.json().catch(() => ({}))) as { isActive?: boolean };
    const updated: GroupSeed = { ...existing, isActive: Boolean(body.isActive) };
    state = state.map((g) => (g.id === id ? updated : g));
    return HttpResponse.json(toDto(updated));
  }),

  http.delete('/api/admin/groups/:id', ({ params }) => {
    const id = String(params['id']);
    const existing = state.find((g) => g.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', 'Guruh topilmadi.');
    if (GROUPS_WITH_DEPENDENTS.has(id)) {
      return problemResponse(
        409,
        'Ziddiyat',
        "Guruhda talabalar yoki biriktirilgan tyutor bor — avval ularni ko'chiring.",
      );
    }
    state = state.filter((g) => g.id !== id);
    return new HttpResponse(null, { status: 204 });
  }),
];

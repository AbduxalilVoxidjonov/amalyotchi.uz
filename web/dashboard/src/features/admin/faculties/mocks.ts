import { http, HttpResponse, type HttpHandler } from 'msw';
import { paginateMock } from '../shared/paginate';
import { FACULTIES_ENDPOINT } from './api';
import type { Faculty, FacultyDto, FacultyInput } from './types';

/** Backend `FacultyRow` shaklida (SPEC-SCREENS §9.3 raqamlari) — boshlang'ich urug' (seed). */
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
    isActive: true,
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
    isActive: true,
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
    isActive: true,
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
    isActive: true,
  },
];

/** Mock holati — POST/PUT/PATCH/DELETE shu ro'yxatni o'zgartiradi. Testlarda `resetFacultiesMock()`. */
let state: Faculty[] = structuredClone(mockFaculties);
let nextId = state.length + 1;

export function resetFacultiesMock() {
  state = structuredClone(mockFaculties);
  nextId = state.length + 1;
}

const PROBLEM_HEADERS = { 'Content-Type': 'application/problem+json' };

function problemResponse(status: number, title: string, detail: string, extra?: object) {
  return HttpResponse.json(
    { status, title, detail, ...extra },
    { status, headers: PROBLEM_HEADERS },
  );
}

/** `FacultyCommandValidator` bilan bir xil (`schema.ts`dagi zod bilan mos). */
function validateInput(body: Partial<FacultyInput>): Record<string, string[]> {
  const errors: Record<string, string[]> = {};
  const name = (body.name ?? '').trim();
  const code = (body.code ?? '').trim();
  if (!name) errors['Name'] = ['Fakultet nomini kiriting.'];
  else if (name.length < 2 || name.length > 150)
    errors['Name'] = ["Fakultet nomi 2–150 belgi bo'lishi kerak."];
  if (!code) errors['Code'] = ['Fakultet kodini kiriting.'];
  else if (!/^[A-Za-z0-9]{2,10}$/.test(code))
    errors['Code'] = ["Fakultet kodi 2–10 ta lotin harf yoki raqamdan iborat bo'lishi kerak."];
  return errors;
}

function toDto(f: Faculty): FacultyDto {
  return { id: f.id, name: f.name, code: f.code, isActive: f.isActive };
}

/** "Bog'liq yozuvlar bor" 409 simulyatsiyasi — 2-fakultet (`f2`, ko'p guruh/tyutor/talaba). */
const FACULTY_WITH_DEPENDENTS = 'f2';

export const facultiesHandlers: HttpHandler[] = [
  http.get(FACULTIES_ENDPOINT, ({ request }) =>
    HttpResponse.json(paginateMock(request.url, state, (f) => [f.name, f.code])),
  ),

  http.post(FACULTIES_ENDPOINT, async ({ request }) => {
    const body = (await request.json().catch(() => ({}))) as Partial<FacultyInput>;
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
    const code = body.code!.trim().toUpperCase();
    if (state.some((f) => f.code.toUpperCase() === code)) {
      return problemResponse(409, 'Ziddiyat', `'${code}' kodli fakultet allaqachon mavjud.`);
    }
    const faculty: Faculty = {
      id: `f${nextId++}`,
      name,
      code,
      directions: 0,
      groups: 0,
      students: 0,
      tutors: 0,
      attendancePct: 0,
      status: 'active',
      isActive: true,
    };
    state = [...state, faculty];
    return HttpResponse.json(toDto(faculty), { status: 201 });
  }),

  http.put(`${FACULTIES_ENDPOINT}/:id`, async ({ request, params }) => {
    const id = String(params['id']);
    const existing = state.find((f) => f.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', 'Fakultet topilmadi.');

    const body = (await request.json().catch(() => ({}))) as Partial<FacultyInput>;
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
    const code = body.code!.trim().toUpperCase();
    if (state.some((f) => f.id !== id && f.code.toUpperCase() === code)) {
      return problemResponse(409, 'Ziddiyat', `'${code}' kodli fakultet allaqachon mavjud.`);
    }
    const updated: Faculty = { ...existing, name, code };
    state = state.map((f) => (f.id === id ? updated : f));
    return HttpResponse.json(toDto(updated));
  }),

  http.patch(`${FACULTIES_ENDPOINT}/:id/status`, async ({ request, params }) => {
    const id = String(params['id']);
    const existing = state.find((f) => f.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', 'Fakultet topilmadi.');

    const body = (await request.json().catch(() => ({}))) as { isActive?: boolean };
    const updated: Faculty = { ...existing, isActive: Boolean(body.isActive) };
    state = state.map((f) => (f.id === id ? updated : f));
    return HttpResponse.json(toDto(updated));
  }),

  http.delete(`${FACULTIES_ENDPOINT}/:id`, ({ params }) => {
    const id = String(params['id']);
    const existing = state.find((f) => f.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', 'Fakultet topilmadi.');
    if (id === FACULTY_WITH_DEPENDENTS) {
      return problemResponse(
        409,
        'Ziddiyat',
        "Fakultetga guruhlar, tyutorlar yoki talabalar biriktirilgan — avval ularni boshqa fakultetga ko'chiring.",
      );
    }
    state = state.filter((f) => f.id !== id);
    return new HttpResponse(null, { status: 204 });
  }),
];

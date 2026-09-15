import { http, HttpResponse, type HttpHandler } from 'msw';
import { mockFaculties } from '../mocks';
import { problemResponse } from '../../shared/mockProblem';
import { paginateMock } from '../../shared/paginate';
import type { DepartmentDto, DepartmentInput, DepartmentRow } from './types';

interface DepartmentSeed extends DepartmentRow {
  facultyId: string;
}

/**
 * Ierarxiya urug'i (hierarchy-contract.md): f1 (Axborot texnologiyalari) — 2 kafedra (biri
 * yo'nalishlar bilan — o'chirish 409, biri bo'sh — o'chirish 204); f2 (Iqtisodiyot va moliya) —
 * shu jumladan `d4` faol emas (holat almashtirish sinovi uchun).
 */
export const mockDepartments: DepartmentSeed[] = [
  {
    id: 'd1',
    facultyId: 'f1',
    name: 'Kompyuter injiniringi kafedrasi',
    code: 'KI',
    isActive: true,
    directions: 2,
    groups: 3,
    students: 43,
  },
  {
    id: 'd2',
    facultyId: 'f1',
    name: 'Axborot xavfsizligi kafedrasi',
    code: 'AX',
    isActive: true,
    directions: 0,
    groups: 0,
    students: 0,
  },
  {
    id: 'd3',
    facultyId: 'f2',
    name: 'Moliya kafedrasi',
    code: 'MOL',
    isActive: true,
    directions: 2,
    groups: 2,
    students: 24,
  },
  {
    id: 'd4',
    facultyId: 'f2',
    name: 'Iqtisodiyot kafedrasi',
    code: 'IQT',
    isActive: false,
    directions: 0,
    groups: 0,
    students: 0,
  },
];

/** O'chirishga qarshilik ko'rsatadigan kafedra (yo'nalishlari bor). */
const DEPARTMENT_WITH_DEPENDENTS = 'd1';

let state: DepartmentSeed[] = structuredClone(mockDepartments);
let nextId = state.length + 1;

export function resetDepartmentsMock() {
  state = structuredClone(mockDepartments);
  nextId = state.length + 1;
}

function facultyName(facultyId: string): string {
  return mockFaculties.find((f) => f.id === facultyId)?.name ?? '';
}

function toDto(d: DepartmentSeed): DepartmentDto {
  return {
    id: d.id,
    facultyId: d.facultyId,
    facultyName: facultyName(d.facultyId),
    name: d.name,
    code: d.code,
    isActive: d.isActive,
  };
}

function toRow(d: DepartmentSeed): DepartmentRow {
  const { facultyId: _facultyId, ...row } = d;
  return row;
}

/** `hierarchy-contract.md` — kafedra/yo'nalish umumiy validatsiya matnlari. */
function validateInput(body: Partial<DepartmentInput>): Record<string, string[]> {
  const errors: Record<string, string[]> = {};
  const name = (body.name ?? '').trim();
  const code = (body.code ?? '').trim();
  if (!name) errors['Name'] = ['Nomni kiriting.'];
  else if (name.length < 2 || name.length > 150)
    errors['Name'] = ["Nom 2–150 belgi bo'lishi kerak."];
  if (!code) errors['Code'] = ['Kodni kiriting.'];
  else if (!/^[A-Za-z0-9-]{2,20}$/.test(code))
    errors['Code'] = ["Kod 2–20 ta lotin harf, raqam yoki '-' dan iborat bo'lishi kerak."];
  return errors;
}

export const departmentsHandlers: HttpHandler[] = [
  http.get('/api/admin/faculties/:facultyId/departments', ({ request, params }) => {
    const facultyId = String(params['facultyId']);
    const rows = state.filter((d) => d.facultyId === facultyId).map(toRow);
    return HttpResponse.json(paginateMock(request.url, rows, (d) => [d.name, d.code]));
  }),

  http.post('/api/admin/faculties/:facultyId/departments', async ({ request, params }) => {
    const facultyId = String(params['facultyId']);
    if (!mockFaculties.some((f) => f.id === facultyId)) {
      return problemResponse(404, 'Topilmadi', 'Fakultet topilmadi.');
    }
    const body = (await request.json().catch(() => ({}))) as Partial<DepartmentInput>;
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
    if (state.some((d) => d.facultyId === facultyId && d.code.toUpperCase() === code)) {
      return problemResponse(
        409,
        'Ziddiyat',
        `'${code}' kodli kafedra bu fakultetda allaqachon mavjud.`,
      );
    }
    const department: DepartmentSeed = {
      id: `d${nextId++}`,
      facultyId,
      name,
      code,
      isActive: true,
      directions: 0,
      groups: 0,
      students: 0,
    };
    state = [...state, department];
    return HttpResponse.json(toDto(department), { status: 201 });
  }),

  http.get('/api/admin/departments/:id', ({ params }) => {
    const id = String(params['id']);
    const existing = state.find((d) => d.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', 'Kafedra topilmadi.');
    return HttpResponse.json(toDto(existing));
  }),

  http.put('/api/admin/departments/:id', async ({ request, params }) => {
    const id = String(params['id']);
    const existing = state.find((d) => d.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', 'Kafedra topilmadi.');

    const body = (await request.json().catch(() => ({}))) as Partial<DepartmentInput>;
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
    if (
      state.some(
        (d) => d.id !== id && d.facultyId === existing.facultyId && d.code.toUpperCase() === code,
      )
    ) {
      return problemResponse(
        409,
        'Ziddiyat',
        `'${code}' kodli kafedra bu fakultetda allaqachon mavjud.`,
      );
    }
    const updated: DepartmentSeed = { ...existing, name, code };
    state = state.map((d) => (d.id === id ? updated : d));
    return HttpResponse.json(toDto(updated));
  }),

  http.patch('/api/admin/departments/:id/status', async ({ request, params }) => {
    const id = String(params['id']);
    const existing = state.find((d) => d.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', 'Kafedra topilmadi.');
    const body = (await request.json().catch(() => ({}))) as { isActive?: boolean };
    const updated: DepartmentSeed = { ...existing, isActive: Boolean(body.isActive) };
    state = state.map((d) => (d.id === id ? updated : d));
    return HttpResponse.json(toDto(updated));
  }),

  http.delete('/api/admin/departments/:id', ({ params }) => {
    const id = String(params['id']);
    const existing = state.find((d) => d.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', 'Kafedra topilmadi.');
    if (id === DEPARTMENT_WITH_DEPENDENTS) {
      return problemResponse(
        409,
        'Ziddiyat',
        "Kafedrada yo'nalishlar bor — avval ularni o'chiring.",
      );
    }
    state = state.filter((d) => d.id !== id);
    return new HttpResponse(null, { status: 204 });
  }),
];

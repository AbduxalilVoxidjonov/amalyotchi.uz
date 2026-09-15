import { http, HttpResponse, type HttpHandler } from 'msw';
import { mockDepartments } from '../departments/mocks';
import { mockFaculties } from '../mocks';
import { problemResponse } from '../../shared/mockProblem';
import { paginateMock } from '../../shared/paginate';
import type { DirectionDto, DirectionInput, DirectionRow } from './types';

interface DirectionSeed extends DirectionRow {
  departmentId: string;
}

/**
 * Ierarxiya urug'i: `d1` ostida 2 yo'nalish — `dir1` (guruhlari bor → o'chirish 409),
 * `dir2` (bitta talabasiz guruh — guruh o'chirish sinovi uchun, o'zi ham "guruhi bor" 409 beradi);
 * `d3` ostida `dir3` (guruhi bor) va `dir4` (guruhi yo'q → o'chirish 204).
 */
export const mockDirections: DirectionSeed[] = [
  {
    id: 'dir1',
    departmentId: 'd1',
    name: 'Kompyuter injiniringi',
    code: 'KI-B',
    isActive: true,
    groups: 2,
    students: 38,
  },
  {
    id: 'dir2',
    departmentId: 'd1',
    name: 'Dasturiy injiniring',
    code: 'DI-B',
    isActive: true,
    groups: 1,
    students: 0,
  },
  {
    id: 'dir3',
    departmentId: 'd3',
    name: 'Bank ishi',
    code: 'BANK-B',
    isActive: true,
    groups: 1,
    students: 24,
  },
  {
    id: 'dir4',
    departmentId: 'd3',
    name: 'Moliyaviy menejment',
    code: 'FM-B',
    isActive: true,
    groups: 0,
    students: 0,
  },
];

/** O'chirishga qarshilik ko'rsatadigan yo'nalish (guruhlari bor). */
const DIRECTION_WITH_DEPENDENTS = 'dir1';

let state: DirectionSeed[] = structuredClone(mockDirections);
let nextId = state.length + 1;

export function resetDirectionsMock() {
  state = structuredClone(mockDirections);
  nextId = state.length + 1;
}

function departmentInfo(departmentId: string) {
  const department = mockDepartments.find((d) => d.id === departmentId);
  const faculty = department ? mockFaculties.find((f) => f.id === department.facultyId) : undefined;
  return {
    departmentName: department?.name ?? '',
    facultyId: department?.facultyId ?? '',
    facultyName: faculty?.name ?? '',
  };
}

function toDto(d: DirectionSeed): DirectionDto {
  const info = departmentInfo(d.departmentId);
  return {
    id: d.id,
    departmentId: d.departmentId,
    departmentName: info.departmentName,
    facultyId: info.facultyId,
    facultyName: info.facultyName,
    name: d.name,
    code: d.code,
    isActive: d.isActive,
  };
}

function toRow(d: DirectionSeed): DirectionRow {
  const { departmentId: _departmentId, ...row } = d;
  return row;
}

/** `hierarchy-contract.md` — kafedra/yo'nalish umumiy validatsiya matnlari. */
function validateInput(body: Partial<DirectionInput>): Record<string, string[]> {
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

export const directionsHandlers: HttpHandler[] = [
  http.get('/api/admin/departments/:departmentId/directions', ({ request, params }) => {
    const departmentId = String(params['departmentId']);
    const rows = state.filter((d) => d.departmentId === departmentId).map(toRow);
    return HttpResponse.json(paginateMock(request.url, rows, (d) => [d.name, d.code]));
  }),

  http.post('/api/admin/departments/:departmentId/directions', async ({ request, params }) => {
    const departmentId = String(params['departmentId']);
    if (!mockDepartments.some((d) => d.id === departmentId)) {
      return problemResponse(404, 'Topilmadi', 'Kafedra topilmadi.');
    }
    const body = (await request.json().catch(() => ({}))) as Partial<DirectionInput>;
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
    if (state.some((d) => d.departmentId === departmentId && d.code.toUpperCase() === code)) {
      return problemResponse(
        409,
        'Ziddiyat',
        `'${code}' kodli yo'nalish bu kafedrada allaqachon mavjud.`,
      );
    }
    const direction: DirectionSeed = {
      id: `dir${nextId++}`,
      departmentId,
      name,
      code,
      isActive: true,
      groups: 0,
      students: 0,
    };
    state = [...state, direction];
    return HttpResponse.json(toDto(direction), { status: 201 });
  }),

  http.get('/api/admin/directions/:id', ({ params }) => {
    const id = String(params['id']);
    const existing = state.find((d) => d.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', "Yo'nalish topilmadi.");
    return HttpResponse.json(toDto(existing));
  }),

  http.put('/api/admin/directions/:id', async ({ request, params }) => {
    const id = String(params['id']);
    const existing = state.find((d) => d.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', "Yo'nalish topilmadi.");

    const body = (await request.json().catch(() => ({}))) as Partial<DirectionInput>;
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
        (d) =>
          d.id !== id && d.departmentId === existing.departmentId && d.code.toUpperCase() === code,
      )
    ) {
      return problemResponse(
        409,
        'Ziddiyat',
        `'${code}' kodli yo'nalish bu kafedrada allaqachon mavjud.`,
      );
    }
    const updated: DirectionSeed = { ...existing, name, code };
    state = state.map((d) => (d.id === id ? updated : d));
    return HttpResponse.json(toDto(updated));
  }),

  http.patch('/api/admin/directions/:id/status', async ({ request, params }) => {
    const id = String(params['id']);
    const existing = state.find((d) => d.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', "Yo'nalish topilmadi.");
    const body = (await request.json().catch(() => ({}))) as { isActive?: boolean };
    const updated: DirectionSeed = { ...existing, isActive: Boolean(body.isActive) };
    state = state.map((d) => (d.id === id ? updated : d));
    return HttpResponse.json(toDto(updated));
  }),

  http.delete('/api/admin/directions/:id', ({ params }) => {
    const id = String(params['id']);
    const existing = state.find((d) => d.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', "Yo'nalish topilmadi.");
    if (id === DIRECTION_WITH_DEPENDENTS) {
      return problemResponse(409, 'Ziddiyat', "Yo'nalishda guruhlar bor — avval ularni o'chiring.");
    }
    state = state.filter((d) => d.id !== id);
    return new HttpResponse(null, { status: 204 });
  }),
];

import { http, HttpResponse, type HttpHandler } from 'msw';
import { mockUsers } from '@/mocks/data';
import { problemResponse } from '../shared/mockProblem';
import { normalizeUzPhone } from '../students/schema';
import { ADMINS_ENDPOINT } from './api';
import type {
  AdminAccount,
  AdminCreateInput,
  AdminCreateResult,
  AdminListResponse,
  AdminUpdateInput,
} from './types';

/**
 * `/api/admin/admins` mock'i (v3.28). Bosh admin — `mockUsers` dagi `isSuperAdmin` (Bearer `sub`
 * orqali aniqlanadi; token bo'lmasa — birinchi foydalanuvchi, ya'ni bosh admin). Oddiy admin → 403.
 * Seed: `mockUsers` dagi adminlar + bitta bloklangan admin.
 */

/** Mock "hozir" — yaratilish vaqti. */
const MOCK_NOW = '2026-10-12T09:00:00+05:00';

function seed(): AdminAccount[] {
  const fromUsers = mockUsers
    .filter((u) => u.role === 1)
    .map((u, i): AdminAccount => ({
      id: u.id,
      fullName: u.fullName,
      login: u.hemisId ?? '',
      phoneNumber: u.phoneNumber,
      isActive: true,
      isSuperAdmin: u.isSuperAdmin === true,
      lastLoginAt: i === 0 ? '2026-10-12T08:41:00+05:00' : '2026-10-10T14:05:00+05:00',
      createdAt: '2026-08-20T10:00:00+05:00',
    }));
  return [
    ...fromUsers,
    {
      id: '11111111-1111-4111-8111-111111111113',
      fullName: 'Rasulov Baxtiyor',
      login: '100000000003',
      phoneNumber: null,
      isActive: false,
      isSuperAdmin: false,
      lastLoginAt: null,
      createdAt: '2026-09-01T09:30:00+05:00',
    },
  ];
}

let admins: AdminAccount[] = seed();
let seq = 0;

export function resetAdminsMock() {
  admins = seed();
  seq = 0;
}

/** Testlar uchun — joriy ro'yxat nusxasi. */
export function mockAdmins(): AdminAccount[] {
  return admins.map((a) => ({ ...a }));
}

function callerId(request: Request): string {
  const auth = request.headers.get('authorization');
  const payload = auth?.startsWith('Bearer ') ? auth.slice(7).split('.')[1] : undefined;
  if (payload) {
    try {
      const { sub } = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/'))) as {
        sub?: string;
      };
      if (sub) return sub;
    } catch {
      /* fallback — bosh admin */
    }
  }
  return mockUsers[0]!.id;
}

function isSuper(request: Request): boolean {
  const id = callerId(request);
  return mockUsers.some((u) => u.id === id && u.role === 1 && u.isSuperAdmin === true);
}

const forbidden = () =>
  problemResponse(403, "Ruxsat yo'q", 'Adminlarni faqat bosh admin boshqara oladi.');
const notFound = () => problemResponse(404, 'Topilmadi', 'Admin topilmadi.');
const conflict = (detail: string) => problemResponse(409, 'Ziddiyat', detail);
const validation = (errors: Record<string, string[]>) =>
  problemResponse(400, "Ma'lumotlar noto'g'ri", Object.values(errors)[0]![0]!, { errors });

/** Mock vaqtinchalik parol (deterministik — testlarda tekshiriladi). */
export function mockTemporaryPassword(n: number): string {
  return `Tmp-${String(4821 + n * 7).padStart(4, '0')}-kx`;
}

function fieldErrors(body: Partial<AdminCreateInput>, create: boolean) {
  const errors: Record<string, string[]> = {};
  const fullName = body.fullName?.trim() ?? '';
  if (fullName.length < 2 || fullName.length > 150)
    errors['FullName'] = ["FISH 2–150 belgi bo'lishi kerak."];
  if (create) {
    const login = body.login?.trim() ?? '';
    if (!login) errors['Login'] = ['Loginni kiriting.'];
    if (body.password && (body.password.length < 8 || body.password.length > 128))
      errors['Password'] = ["Parol 8–128 ta belgidan iborat bo'lishi kerak."];
  }
  if (body.phoneNumber && !normalizeUzPhone(body.phoneNumber))
    errors['PhoneNumber'] = ["Telefon raqami noto'g'ri."];
  return errors;
}

export const adminsHandlers: HttpHandler[] = [
  http.get(ADMINS_ENDPOINT, ({ request }) => {
    if (!isSuper(request)) return forbidden();
    const body: AdminListResponse = { items: mockAdmins() };
    return HttpResponse.json(body);
  }),

  http.post(ADMINS_ENDPOINT, async ({ request }) => {
    if (!isSuper(request)) return forbidden();
    const body = (await request.json().catch(() => ({}))) as Partial<AdminCreateInput>;
    const errors = fieldErrors(body, true);
    if (Object.keys(errors).length > 0) return validation(errors);
    const login = body.login!.trim();
    const taken =
      admins.some((a) => a.login === login) || mockUsers.some((u) => u.hemisId === login);
    if (taken) return conflict('Bu login allaqachon band.');

    seq += 1;
    const created: AdminAccount = {
      id: `adm-new-${seq}`,
      fullName: body.fullName!.trim(),
      login,
      phoneNumber: body.phoneNumber ? normalizeUzPhone(body.phoneNumber) : null,
      isActive: true,
      isSuperAdmin: false,
      lastLoginAt: null,
      createdAt: MOCK_NOW,
    };
    admins = [...admins, created];
    const result: AdminCreateResult = body.password
      ? created
      : { ...created, temporaryPassword: mockTemporaryPassword(seq) };
    return HttpResponse.json(result, { status: 201 });
  }),

  http.put(`${ADMINS_ENDPOINT}/:id`, async ({ request, params }) => {
    if (!isSuper(request)) return forbidden();
    const id = String(params['id']);
    const current = admins.find((a) => a.id === id);
    if (!current) return notFound();
    const body = (await request.json().catch(() => ({}))) as Partial<AdminUpdateInput>;
    const errors = fieldErrors(body, false);
    if (Object.keys(errors).length > 0) return validation(errors);
    const next: AdminAccount = {
      ...current,
      fullName: body.fullName!.trim(),
      phoneNumber: body.phoneNumber ? normalizeUzPhone(body.phoneNumber) : null,
    };
    admins = admins.map((a) => (a.id === id ? next : a));
    return HttpResponse.json(next);
  }),

  http.patch(`${ADMINS_ENDPOINT}/:id/status`, async ({ request, params }) => {
    if (!isSuper(request)) return forbidden();
    const id = String(params['id']);
    const current = admins.find((a) => a.id === id);
    if (!current) return notFound();
    const body = (await request.json().catch(() => ({}))) as { isActive?: boolean };
    if (typeof body.isActive !== 'boolean') return validation({ IsActive: ['Holat majburiy.'] });
    if (!body.isActive && current.isSuperAdmin) return conflict("Bosh adminni bloklab bo'lmaydi.");
    if (!body.isActive && id === callerId(request))
      return conflict("O'zingizni bloklab bo'lmaydi.");
    const next = { ...current, isActive: body.isActive };
    admins = admins.map((a) => (a.id === id ? next : a));
    return HttpResponse.json(next);
  }),

  http.post(`${ADMINS_ENDPOINT}/:id/password`, ({ request, params }) => {
    if (!isSuper(request)) return forbidden();
    const id = String(params['id']);
    if (!admins.some((a) => a.id === id)) return notFound();
    seq += 1;
    return HttpResponse.json({ temporaryPassword: mockTemporaryPassword(seq) });
  }),
];

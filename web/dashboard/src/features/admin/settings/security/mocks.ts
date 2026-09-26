import { http, HttpResponse, type HttpHandler } from 'msw';
import { ACCOUNT_ENDPOINTS, type ChangeLoginRequest } from '@/features/auth/api';
import { mockStudents } from '@/features/admin/students/mocks';
import { mockUsers, type MockUser } from '@/mocks/data';
import { problemResponse } from '../../shared/mockProblem';

/**
 * "Hisob xavfsizligi" mock'lari — backend `AuthController` kontrakti:
 *   POST /api/auth/change-password → 204 · 400 errors.CurrentPassword / errors.NewPassword
 *   GET  /api/auth/login-available?login= → 200 { available, normalized, reason }
 *   POST /api/auth/change-login → 200 UserSummaryDto · 400 errors.CurrentPassword / errors.NewLogin · 409
 *
 * Joriy foydalanuvchi — Bearer token `sub`i; token bo'lmasa (store'siz testlar) — mock admin.
 * `mockUsers` o'zgartiriladi (login/me mock'lari mos qolsin) → testlarda `resetAccountSecurityMock()`.
 */

export const WRONG_CURRENT_PASSWORD = "Joriy parol noto'g'ri.";
export const LOGIN_TAKEN = 'Bu login allaqachon band.';
export const LOGIN_FORMAT =
  "Login faqat raqamlardan iborat, 5–20 belgi bo'lishi kerak (HEMIS ID formati).";
export const LOGIN_OWN = 'Bu sizning joriy loginingiz.';
export const LOGIN_SAME = 'Yangi login joriy logindan farq qilishi kerak.';

const LOGIN_RE = /^\d{5,20}$/;
const PASSWORD_MIN = 8;
const PASSWORD_MAX = 128;

const snapshot = mockUsers.map((u) => ({ id: u.id, hemisId: u.hemisId, password: u.password }));

export function resetAccountSecurityMock() {
  for (const s of snapshot) {
    const u = mockUsers.find((x) => x.id === s.id);
    if (u) {
      u.hemisId = s.hemisId ?? null;
      u.password = s.password;
    }
  }
}

function currentUser(request: Request): MockUser {
  const auth = request.headers.get('authorization');
  const token = auth?.startsWith('Bearer ') ? auth.slice(7) : null;
  const payload = token?.split('.')[1];
  if (payload) {
    try {
      const { sub } = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/'))) as {
        sub?: string;
      };
      const user = mockUsers.find((u) => u.id === sub);
      if (user) return user;
    } catch {
      /* fallback — admin */
    }
  }
  return mockUsers[0]!;
}

/** Band loginlar: mock admin/tyutor/talaba foydalanuvchilari + talabalar ro'yxati HEMIS ID'lari. */
function isTaken(login: string, exceptUserId: string): boolean {
  if (mockUsers.some((u) => u.id !== exceptUserId && u.hemisId === login)) return true;
  return mockStudents.some((s) => s.hemisId === login);
}

function validation(errors: Record<string, string[]>) {
  return problemResponse(400, "Ma'lumotlar noto'g'ri", Object.values(errors)[0]![0]!, { errors });
}

export const accountSecurityHandlers: HttpHandler[] = [
  http.post(ACCOUNT_ENDPOINTS.changePassword, async ({ request }) => {
    const body = (await request.json().catch(() => ({}))) as {
      currentPassword?: string;
      newPassword?: string;
    };
    const user = currentUser(request);
    const current = body.currentPassword ?? '';
    const next = body.newPassword ?? '';
    if (!current) return validation({ CurrentPassword: ['Joriy parolni kiriting.'] });
    if (next.length < PASSWORD_MIN || next.length > PASSWORD_MAX)
      return validation({
        NewPassword: [`Parol ${PASSWORD_MIN}–${PASSWORD_MAX} ta belgidan iborat bo'lishi kerak.`],
      });
    if (next === current)
      return validation({ NewPassword: ['Yangi parol joriy paroldan farq qilishi kerak.'] });
    if (current !== user.password) return validation({ CurrentPassword: [WRONG_CURRENT_PASSWORD] });
    user.password = next;
    return new HttpResponse(null, { status: 204 });
  }),

  http.get(ACCOUNT_ENDPOINTS.loginAvailable, ({ request }) => {
    const raw = new URL(request.url).searchParams.get('login') ?? '';
    const normalized = raw.trim();
    const user = currentUser(request);
    if (!LOGIN_RE.test(normalized))
      return HttpResponse.json({ available: false, normalized, reason: LOGIN_FORMAT });
    if (normalized === user.hemisId)
      return HttpResponse.json({ available: false, normalized, reason: LOGIN_OWN });
    if (isTaken(normalized, user.id))
      return HttpResponse.json({ available: false, normalized, reason: LOGIN_TAKEN });
    return HttpResponse.json({ available: true, normalized, reason: null });
  }),

  http.post(ACCOUNT_ENDPOINTS.changeLogin, async ({ request }) => {
    const body = (await request.json().catch(() => ({}))) as Partial<ChangeLoginRequest>;
    const user = currentUser(request);
    const login = (body.newLogin ?? '').trim();
    if (!LOGIN_RE.test(login)) return validation({ NewLogin: [LOGIN_FORMAT] });
    if (login === user.hemisId) return validation({ NewLogin: [LOGIN_SAME] });
    if (body.currentPassword !== user.password)
      return validation({ CurrentPassword: [WRONG_CURRENT_PASSWORD] });
    if (isTaken(login, user.id)) return problemResponse(409, 'Konflikt', LOGIN_TAKEN);
    user.hemisId = login;
    const { password: _password, ...summary } = user;
    return HttpResponse.json(summary);
  }),
];

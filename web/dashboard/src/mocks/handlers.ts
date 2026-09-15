import { http, HttpResponse, type HttpHandler } from 'msw';
import type { LoginRequest, LogoutRequest, RefreshRequest } from '@amaliyotchi/shared';
import { adminHandlers } from '@/features/admin/mocks';
import { reportsHandlers } from '@/features/reports/mocks';
import { tutorHandlers } from '@/features/tutor/mocks';
import { issueSession, mockSessions, mockUsers, problem } from './data';

/**
 * Auth mock'lari — backend AuthController bilan bir xil shakl:
 *   POST /api/auth/login   → 200 AuthResultDto | 400 validation | 403 forbidden
 *   POST /api/auth/refresh → 200 AuthResultDto (rotatsiya) | 403
 *   POST /api/auth/logout  → 204
 *   GET  /api/auth/me      → 200 UserSummaryDto | 401
 *
 * Keyingi feature'lar (students, places, reports...) o'z `*.handlers.ts` faylini
 * `src/features/<name>/mocks.ts` da yozib, shu ro'yxatga qo'shadi.
 */

const PROBLEM_HEADERS = { 'Content-Type': 'application/problem+json' };

function problemResponse(status: number, title: string, detail: string, extra?: object) {
  return HttpResponse.json(problem(status, title, detail, extra), {
    status,
    headers: PROBLEM_HEADERS,
  });
}

function bearerUserId(request: Request): string | null {
  const auth = request.headers.get('authorization');
  if (!auth?.startsWith('Bearer ')) return null;
  const token = auth.slice('Bearer '.length);
  const parts = token.split('.');
  if (parts.length !== 3 || !parts[1]) return null;
  try {
    const json = atob(parts[1].replace(/-/g, '+').replace(/_/g, '/'));
    const payload = JSON.parse(json) as { sub?: string; exp?: number };
    if (!payload.sub) return null;
    if (payload.exp && payload.exp * 1000 <= Date.now()) return null;
    return payload.sub;
  } catch {
    return null;
  }
}

export const authHandlers: HttpHandler[] = [
  http.post('/api/auth/login', async ({ request }) => {
    const body = (await request.json().catch(() => ({}))) as Partial<LoginRequest>;
    const errors: Record<string, string[]> = {};
    if (!body.hemisId) errors['HemisId'] = ['HEMIS ID ni kiriting.'];
    if (!body.password) errors['Password'] = ['Parolni kiriting.'];
    else if (body.password.length < 8)
      errors['Password'] = ["Parol kamida 8 ta belgidan iborat bo'lishi kerak."];
    if (Object.keys(errors).length > 0) {
      return problemResponse(400, "Ma'lumotlar noto'g'ri", 'Bir yoki bir nechta maydon xato.', {
        errors,
      });
    }

    const user = mockUsers.find((u) => u.hemisId === body.hemisId);
    if (!user || user.password !== body.password || user.role === 3) {
      return problemResponse(403, "Ruxsat yo'q", "HEMIS ID yoki parol noto'g'ri.");
    }
    return HttpResponse.json(issueSession(user));
  }),

  http.post('/api/auth/refresh', async ({ request }) => {
    const body = (await request.json().catch(() => ({}))) as Partial<RefreshRequest>;
    const userId = body.refreshToken ? mockSessions.get(body.refreshToken) : undefined;
    const user = userId ? mockUsers.find((u) => u.id === userId) : undefined;
    if (!body.refreshToken || !user) {
      return problemResponse(403, "Ruxsat yo'q", 'Sessiya muddati tugagan. Qaytadan kiring.');
    }
    mockSessions.delete(body.refreshToken); // rotatsiya
    return HttpResponse.json(issueSession(user));
  }),

  http.post('/api/auth/logout', async ({ request }) => {
    if (!bearerUserId(request)) return new HttpResponse(null, { status: 401 });
    const body = (await request.json().catch(() => ({}))) as Partial<LogoutRequest>;
    if (body.refreshToken) mockSessions.delete(body.refreshToken);
    return new HttpResponse(null, { status: 204 });
  }),

  http.get('/api/auth/me', ({ request }) => {
    const userId = bearerUserId(request);
    const user = userId ? mockUsers.find((u) => u.id === userId) : undefined;
    if (!user) return new HttpResponse(null, { status: 401 });
    const { password: _password, ...summary } = user;
    return HttpResponse.json(summary);
  }),
];

export const handlers: HttpHandler[] = [
  ...authHandlers,
  ...adminHandlers,
  ...reportsHandlers,
  ...tutorHandlers,
];

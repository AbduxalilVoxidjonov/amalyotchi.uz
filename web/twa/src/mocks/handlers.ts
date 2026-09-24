import { http, HttpResponse, type HttpHandler } from 'msw';
import { calendarHandlers } from '@/features/calendar/mocks';
import { diaryHandlers } from '@/features/diary/mocks';
import { leaveHandlers } from '@/features/leave/mocks';
import { placeHandlers } from '@/features/place/mocks';
import { portfolioHandlers } from '@/features/portfolio/mocks';
import { profileHandlers } from '@/features/profile/mocks';
import { todayHandlers } from '@/features/today/mocks';
import {
  accountByUserId,
  accountFromRequest,
  issueSession,
  mockAccounts,
  mockSessions,
  mockStudent,
} from './data';
import { forbidden, problem, unauthorized } from './problem';

/**
 * TWA auth mock'lari (backend `TelegramLoginCommand` bilan bir xil shakl).
 *   POST /api/auth/telegram { initData } → AuthResultDto | 403 ProblemDetails
 *   POST /api/auth/refresh  { refreshToken } → AuthResultDto
 *   GET  /api/auth/me → UserSummaryDto
 *   POST /api/auth/login { hemisId, password } → AuthResultDto (+ mustChangePassword) | 403 `detail` (kontrakt v3.8)
 *        (hisoblar — `mockAccounts` (data.ts): 341030/talaba12345 · 341031/vaqtincha1 · 100000000002/tyutor123)
 *   POST /api/auth/logout { refreshToken } → 204
 *   POST /api/auth/change-password { currentPassword, newPassword, refreshToken? } → 204 | 400 errors.CurrentPassword/NewPassword
 * Mock rejimida initData bo'sh bo'lsa ham (oddiy brauzer) kirishga ruxsat beriladi —
 * `initData === 'invalid'` → 403 "imzo", `'unlinked'` → 403 "hisob topilmadi" (xato oqimini sinash uchun).
 *
 * Talaba endpointlari (`/api/student/*`) — har feature'ning `mocks.ts` faylida (kontrakt v2 shakli).
 */
const authHandlers: HttpHandler[] = [
  http.post('/api/auth/telegram', async ({ request }) => {
    const body = (await request.json().catch(() => ({}))) as { initData?: string };
    if (body.initData === 'invalid') {
      return forbidden('Telegram imzosi tasdiqlanmadi. Ilovani qaytadan oching.');
    }
    if (body.initData === 'unlinked') {
      return forbidden('Hisob topilmadi — tyutoringizdan taklif havolasini oling.');
    }
    return HttpResponse.json(issueSession(mockStudent));
  }),

  http.post('/api/auth/refresh', async ({ request }) => {
    const body = (await request.json().catch(() => ({}))) as { refreshToken?: string };
    if (!body.refreshToken || !mockSessions.has(body.refreshToken)) {
      return forbidden('Sessiya muddati tugagan. Qaytadan kiring.');
    }
    const userId = mockSessions.get(body.refreshToken);
    mockSessions.delete(body.refreshToken);
    return HttpResponse.json(issueSession(accountByUserId(userId)?.user ?? mockStudent));
  }),

  http.post('/api/auth/login', async ({ request }) => {
    const body = (await request.json().catch(() => ({}))) as {
      hemisId?: string;
      password?: string;
    };
    const account = mockAccounts.get((body.hemisId ?? '').trim());
    if (!account || account.password !== body.password) {
      return forbidden("HEMIS ID yoki parol noto'g'ri.");
    }
    return HttpResponse.json(issueSession(account.user));
  }),

  http.post('/api/auth/logout', async ({ request }) => {
    if (!request.headers.get('authorization')?.startsWith('Bearer ')) return unauthorized();
    const body = (await request.json().catch(() => ({}))) as { refreshToken?: string };
    if (body.refreshToken) mockSessions.delete(body.refreshToken);
    return new HttpResponse(null, { status: 204 });
  }),

  http.post('/api/auth/change-password', async ({ request }) => {
    const account = accountFromRequest(request);
    if (!account) return unauthorized();
    const body = (await request.json().catch(() => ({}))) as {
      currentPassword?: string;
      newPassword?: string;
      refreshToken?: string;
    };
    const errors: Record<string, string[]> = {};
    if (body.currentPassword !== account.password) {
      errors['CurrentPassword'] = ["Joriy parol noto'g'ri."];
    }
    const next = body.newPassword ?? '';
    if (next.length < 8)
      errors['NewPassword'] = ["Parol kamida 8 ta belgidan iborat bo'lishi kerak."];
    else if (next === body.currentPassword) {
      errors['NewPassword'] = ['Yangi parol joriy paroldan farq qilishi kerak.'];
    }
    if (Object.keys(errors).length > 0) {
      return problem(400, "Ma'lumotlar noto'g'ri", "Ma'lumotlar noto'g'ri.", { errors });
    }
    account.password = next;
    account.mustChangePassword = false;
    // Backend kabi: boshqa sessiyalar bekor, yuborilgan joriy refresh token saqlanadi.
    for (const [token, userId] of mockSessions) {
      if (userId === account.user.id && token !== body.refreshToken) mockSessions.delete(token);
    }
    return new HttpResponse(null, { status: 204 });
  }),

  http.get('/api/auth/me', ({ request }) => {
    if (!request.headers.get('authorization')?.startsWith('Bearer ')) return unauthorized();
    return HttpResponse.json(mockStudent);
  }),
];

export const handlers: HttpHandler[] = [
  ...authHandlers,
  ...todayHandlers,
  ...placeHandlers,
  ...diaryHandlers,
  ...calendarHandlers,
  ...leaveHandlers,
  ...portfolioHandlers,
  ...profileHandlers,
];

import { http, HttpResponse, type HttpHandler } from 'msw';
import { calendarHandlers } from '@/features/calendar/mocks';
import { diaryHandlers } from '@/features/diary/mocks';
import { leaveHandlers } from '@/features/leave/mocks';
import { placeHandlers } from '@/features/place/mocks';
import { portfolioHandlers } from '@/features/portfolio/mocks';
import { todayHandlers } from '@/features/today/mocks';
import { issueSession, mockSessions, mockStudent } from './data';
import { forbidden, unauthorized } from './problem';

/**
 * TWA auth mock'lari (backend `TelegramLoginCommand` bilan bir xil shakl).
 *   POST /api/auth/telegram { initData } → AuthResultDto | 403 ProblemDetails
 *   POST /api/auth/refresh  { refreshToken } → AuthResultDto
 *   GET  /api/auth/me → UserSummaryDto
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
    mockSessions.delete(body.refreshToken);
    return HttpResponse.json(issueSession(mockStudent));
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
];

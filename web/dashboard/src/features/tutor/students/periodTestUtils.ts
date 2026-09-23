import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { buildDetail } from './mocks';
import type { StudentPeriod, StudentPeriodOption, TutorStudentDetail } from './types';

/**
 * Talaba profili testlari (admin va tyutor) uchun davr fixture'lari — faqat testlarda import qilinadi.
 */

/** Tugagan, lekin yopilmagan davr (backend `active` qaytaradi) — frontend "Tugagan" deb ko'rsatadi. */
export const ENDED_PERIOD: StudentPeriod = {
  id: 'per-2026-bahor',
  name: 'Bahorgi amaliyot 2026',
  startDate: '2026-02-02',
  endDate: '2026-04-30',
  dailyStart: '09:00',
  dailyEnd: '17:00',
  workDays: [1, 2, 3, 4, 5],
  requiredDays: 60,
};

export const ENDED_OPTIONS: StudentPeriodOption[] = [
  { ...pick(ENDED_PERIOD), status: 'active', isDefault: true },
  {
    id: 'per-2025-kuz',
    name: 'Kuzgi amaliyot 2025',
    startDate: '2025-09-01',
    endDate: '2025-11-28',
    status: 'closed',
    isDefault: false,
  },
];

function pick(p: StudentPeriod) {
  return { id: p.id, name: p.name, startDate: p.startDate, endDate: p.endDate };
}

/** Demo profilni berilgan davrlar bilan qaytaradi (`buildDetail('s-341030')` asosida). */
export function detailWithPeriods(
  periods: StudentPeriodOption[],
  period: StudentPeriod | null,
): TutorStudentDetail {
  const base = buildDetail('s-341030');
  if (!base || base === 'periodNotFound') throw new Error('mock profil topilmadi');
  return { ...base, periods, period, selectedPeriodId: period?.id ?? null };
}

/**
 * `GET /api/{area}/students/:id` ni almashtiradi; `/attendance` bo'sh qaytaradi.
 * `extra` — admin profili maydonlari (javobga qo'shiladi).
 */
export function mockProfilePeriods(
  area: 'tutor' | 'admin',
  detail: TutorStudentDetail,
  extra: Record<string, unknown> = {},
) {
  server.use(
    http.get(`/api/${area}/students/:id`, ({ params }) =>
      HttpResponse.json({ ...detail, ...extra, id: String(params['id']) }),
    ),
    http.get(`/api/${area}/students/:id/attendance`, () => HttpResponse.json([])),
    http.get(`/api/${area}/students/:id/diaries`, () => HttpResponse.json([])),
  );
}

/** MSW orqali ketgan so'rov URL'lari (tekshirish uchun); `stop()` tinglashni to'xtatadi. */
export function recordRequests() {
  const urls: URL[] = [];
  const listener = ({ request }: { request: Request }) => {
    urls.push(new URL(request.url));
  };
  server.events.on('request:start', listener);
  return {
    urls,
    stop: () => server.events.removeListener('request:start', listener),
    /** Yo'li `pathname` ga teng so'rovlarning `periodId` qiymatlari. */
    periodIds: (pathname: string) =>
      urls.filter((u) => u.pathname === pathname).map((u) => u.searchParams.get('periodId')),
  };
}

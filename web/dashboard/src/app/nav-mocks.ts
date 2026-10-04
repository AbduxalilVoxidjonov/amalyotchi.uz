import { http, HttpResponse, type HttpHandler } from 'msw';
import { mockApplications } from '@/features/admin/applications/mocks';
import { COMPANIES_ENDPOINT } from '@/features/admin/companies/api';
import { mockDiaries } from '@/features/admin/diaries/mocks';
import { FACULTIES_ENDPOINT } from '@/features/admin/faculties/api';
import { pendingFaceCount } from '@/features/admin/face/mocks';
import { STUDENTS_ENDPOINT } from '@/features/admin/students/api';
import { mockTodayRows } from '@/features/admin/today/mocks';
import { NAV_ENDPOINT, type AdminNavDto } from './nav-data';

/**
 * `GET /api/admin/nav` mock'i. Sonlar qattiq yozilmaydi:
 *  - fakultet/korxona/talaba: ro'yxat endpoint'larining (mutatsiyalar o'zgartirgan in-memory holat)
 *    `total` qiymati — o'sha handler'ning o'zidan (`?pageSize=1`, sukut holati) o'qiladi.
 *  - bugun/arizalar/kundaliklar/yuz: mock massivlar uzunligi, sahifalarning sukut filtri bilan
 *    (bugun — hammasi; arizalar — "Yangi" (`submitted`) tab; kundaliklar — hammasi; yuz — kutayotgan).
 *    `mockApplications`/`mockDiaries` — `export let` (live binding): qaror/bahodan keyin ham mos.
 */

/** ❓ Faol o'quv yili (mock). */
const MOCK_ACADEMIC_YEAR = '2026-2027';

async function listTotal(requestUrl: string, endpoint: string): Promise<number> {
  const url = new URL(endpoint, requestUrl);
  url.searchParams.set('pageSize', '1');
  const res = await fetch(url);
  const body = (await res.json()) as { total: number };
  return body.total;
}

/** Sobiq tyutor nav sonlari (v3.28 da `counts` ga qo'shildi) — mock holatidan. */
export function opsNavCounts() {
  return {
    today: mockTodayRows.length,
    applications: mockApplications.filter((a) => a.status === 'submitted').length,
    diaries: mockDiaries.length,
    pendingFaceEnrollments: pendingFaceCount(),
  };
}

export const navHandlers: HttpHandler[] = [
  http.get(NAV_ENDPOINT, async ({ request }) => {
    const [faculties, companies, students] = await Promise.all([
      listTotal(request.url, FACULTIES_ENDPOINT),
      listTotal(request.url, COMPANIES_ENDPOINT),
      listTotal(request.url, STUDENTS_ENDPOINT),
    ]);
    const body: AdminNavDto = {
      counts: { faculties, companies, students, ...opsNavCounts() },
      context: { academicYear: MOCK_ACADEMIC_YEAR },
    };
    return HttpResponse.json(body);
  }),
];

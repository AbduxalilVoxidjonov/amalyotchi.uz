import { http, HttpResponse, type HttpHandler } from 'msw';
import { UserRole } from '@amaliyotchi/shared/auth';
import { COMPANIES_ENDPOINT } from '@/features/admin/companies/api';
import { FACULTIES_ENDPOINT } from '@/features/admin/faculties/api';
import { STUDENTS_ENDPOINT } from '@/features/admin/students/api';
import { TUTORS_ENDPOINT } from '@/features/admin/tutors/api';
import { mockApplications } from '@/features/tutor/applications/mocks';
import { mockDiaries } from '@/features/tutor/diaries/mocks';
import { pendingFaceCount } from '@/features/tutor/face/mocks';
import {
  mockStudents as tutorMockStudents,
  resolveMockPeriod,
} from '@/features/tutor/students/mocks';
import { mockTodayRows } from '@/features/tutor/today/mocks';
import { NAV_ENDPOINTS, type AdminNavDto, type TutorNavDto } from './nav-data';

/**
 * `GET /api/admin/nav`, `GET /api/tutor/nav` mock'lari. Sonlar qattiq yozilmaydi:
 *  - admin: ro'yxat endpoint'larining (mutatsiyalar o'zgartirgan in-memory holat) `total` qiymati —
 *    feature mock'lari holat massivini eksport qilmaydi, shuning uchun o'sha handler'ning o'zidan
 *    (`?pageSize=1`, sukut holati — qidiruv/filtrsiz) o'qiladi; sahifa jamiga aynan teng.
 *  - tyutor: mock massivlar uzunligi, sahifalarning sukut filtri bilan
 *    (bugun — hammasi; arizalar — "Yangi" (`submitted`) tab; talabalar — hammasi; kundaliklar — hammasi).
 *    `mockApplications`/`mockDiaries` — `export let` (live binding): qaror/bahodan keyin ham mos.
 */

/** ❓ Faol o'quv yili — tyutorlar mock'idagi `ACADEMIC_YEAR` bilan bir xil (u eksport qilinmaydi). */
const MOCK_ACADEMIC_YEAR = '2026-2027';

async function listTotal(requestUrl: string, endpoint: string): Promise<number> {
  const url = new URL(endpoint, requestUrl);
  url.searchParams.set('pageSize', '1');
  const res = await fetch(url);
  const body = (await res.json()) as { total: number };
  return body.total;
}

export function tutorNavMock(): TutorNavDto {
  const groups = [...new Set(tutorMockStudents.map((s) => s.group))].sort();
  const first = tutorMockStudents[0];
  return {
    counts: {
      today: mockTodayRows.length,
      applications: mockApplications.filter((a) => a.status === 'submitted').length,
      students: tutorMockStudents.length,
      diaries: mockDiaries.length,
      pendingFaceEnrollments: pendingFaceCount(),
    },
    context: {
      groups,
      periodName: (first && resolveMockPeriod(first.id, null)?.name) ?? null,
    },
  };
}

export const navHandlers: HttpHandler[] = [
  http.get(NAV_ENDPOINTS[UserRole.Admin], async ({ request }) => {
    const [faculties, tutors, companies, students] = await Promise.all([
      listTotal(request.url, FACULTIES_ENDPOINT),
      listTotal(request.url, TUTORS_ENDPOINT),
      listTotal(request.url, COMPANIES_ENDPOINT),
      listTotal(request.url, STUDENTS_ENDPOINT),
    ]);
    const body: AdminNavDto = {
      counts: { faculties, tutors, companies, students },
      context: { academicYear: MOCK_ACADEMIC_YEAR },
    };
    return HttpResponse.json(body);
  }),

  http.get(NAV_ENDPOINTS[UserRole.Tutor], () => HttpResponse.json(tutorNavMock())),
];

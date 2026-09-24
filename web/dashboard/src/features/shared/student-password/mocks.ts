import { http, HttpResponse, type HttpHandler } from 'msw';
import { mockStudents as adminMockStudents } from '@/features/admin/students/mocks';
import { mockStudents as tutorMockStudents } from '@/features/tutor/students/mocks';
import { problem } from '@/mocks/data';
import { setMockStudentPassword } from './passwordStore';

export { hasMockStudentPassword, resetStudentPasswordMock } from './passwordStore';

/** Ushbu parol mock'da serverda rad etiladi (400) — UI server xatosini ko'rsatishini sinash uchun. */
export const MOCK_REJECTED_STUDENT_PASSWORD = 'parol12345';

function validation(errors: Record<string, string[]>) {
  return HttpResponse.json(
    problem(400, 'One or more validation errors occurred.', '', { errors }),
    {
      status: 400,
      headers: { 'Content-Type': 'application/problem+json' },
    },
  );
}

const notFound = () =>
  HttpResponse.json(problem(404, 'Topilmadi', 'Talaba topilmadi.'), { status: 404 });

function handler(area: 'admin' | 'tutor', exists: (id: string) => boolean) {
  return http.post(`/api/${area}/students/:id/password`, async ({ request, params }) => {
    const id = String(params['id']);
    if (!exists(id)) return notFound();
    const body = (await request.json().catch(() => ({}))) as { password?: string };
    const password = body.password ?? '';
    if (password.length < 8)
      return validation({ password: ["Parol kamida 8 ta belgidan iborat bo'lishi kerak."] });
    if (password.length > 128)
      return validation({ password: ['Parol 128 ta belgidan oshmasligi kerak.'] });
    if (password === MOCK_REJECTED_STUDENT_PASSWORD)
      return validation({ password: ['Parol juda oddiy. Boshqasini tanlang.'] });
    setMockStudentPassword(id);
    return new HttpResponse(null, { status: 204 });
  });
}

/** `POST /api/{admin|tutor}/students/{id}/password` mock'lari (`src/mocks/handlers.ts` ga ulanadi). */
export const studentPasswordHandlers: HttpHandler[] = [
  handler('admin', (id) => adminMockStudents.some((s) => s.id === id)),
  handler('tutor', (id) => tutorMockStudents.some((s) => s.id === id)),
];

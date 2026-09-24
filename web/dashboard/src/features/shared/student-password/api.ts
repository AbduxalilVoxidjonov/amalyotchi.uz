import { api } from '@/shared/api';

/** Qaysi bo'lim endpoint'i: admin (`AdminStudentsController`) yoki tyutor (`TutorStudentsController`). */
export type StudentPasswordArea = 'admin' | 'tutor';

/**
 * Talaba parolini o'rnatish (talabaning o'z paroli yo'q — admin/tyutor beradi):
 *   POST /api/admin/students/{id}/password  { password } → 204 · 400 `errors.password` · 404
 *   POST /api/tutor/students/{id}/password  { password } → 204 · 400 · 404 (ko'lamdan tashqari)
 * Talaba birinchi kirishda parolni o'zgartiradi (`AuthResultDto.mustChangePassword`).
 */
export const studentPasswordEndpoint = (area: StudentPasswordArea, studentId: string) =>
  `/api/${area}/students/${encodeURIComponent(studentId)}/password`;

export const studentPasswordApi = {
  set: (area: StudentPasswordArea, studentId: string, password: string) =>
    api.post<void>(studentPasswordEndpoint(area, studentId), { password }),
};

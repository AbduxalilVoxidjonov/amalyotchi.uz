import { api } from '@/shared/api';

/**
 * Talaba parolini o'rnatish (talabaning o'z paroli yo'q — admin beradi):
 *   POST /api/admin/students/{id}/password  { password } → 204 · 400 `errors.password` · 404
 * Talaba birinchi kirishda parolni o'zgartiradi (`AuthResultDto.mustChangePassword`).
 */
export const studentPasswordEndpoint = (studentId: string) =>
  `/api/admin/students/${encodeURIComponent(studentId)}/password`;

export const studentPasswordApi = {
  set: (studentId: string, password: string) =>
    api.post<void>(studentPasswordEndpoint(studentId), { password }),
};

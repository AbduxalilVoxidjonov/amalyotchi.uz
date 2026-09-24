import type { HttpHandler } from 'msw';
import { resetStudentPasswordMock } from '@/features/shared/student-password/passwordStore';
import { applicationsHandlers, resetApplicationsMock } from './applications/mocks';
import { calendarHandlers } from './calendar/mocks';
import { companiesHandlers } from './companies/mocks';
import { diariesHandlers, resetDiariesMock } from './diaries/mocks';
import { gradingHandlers, resetGradingMock } from './grading/mocks';
import { mapHandlers } from './map/mocks';
import { resetStudentDiaryReviewsMock, studentsHandlers } from './students/mocks';
import { todayHandlers } from './today/mocks';

/**
 * Tyutor bo'limi MSW handler'lari — `src/mocks/handlers.ts` ro'yxatiga `...tutorHandlers` sifatida qo'shiladi.
 * Shakl backend v2 DTO'lari bilan bir xil (`src/Amaliyotchi.Application/Features/Tutor/**`); kontrakt — har entity'ning `api.ts` izohida.
 */
export const tutorHandlers: HttpHandler[] = [
  ...todayHandlers,
  ...applicationsHandlers,
  ...studentsHandlers,
  ...diariesHandlers,
  ...calendarHandlers,
  ...mapHandlers,
  ...gradingHandlers,
  ...companiesHandlers,
];

/** Mutatsiyalar o'zgartirgan in-memory holatni tiklash (testlar orasida). */
export function resetTutorMocks() {
  resetApplicationsMock();
  resetDiariesMock();
  resetGradingMock();
  resetStudentDiaryReviewsMock();
  // Talaba paroli (admin va tyutor profili umumiy mock holati).
  resetStudentPasswordMock();
}

import type { HttpHandler } from 'msw';
import { resetStudentPasswordMock } from '@/features/shared/student-password/passwordStore';
import { applicationsHandlers, resetApplicationsMock } from './applications/mocks';
import { calendarHandlers } from './calendar/mocks';
import { diariesHandlers, resetDiariesMock } from './diaries/mocks';
import { resetFaceMock } from './face/mockStore';
import { faceHandlers } from './face/mocks';
import { gradingHandlers, resetGradingMock } from './grading/mocks';
import { locationHandlers } from './location/mocks';
import { mapHandlers } from './map/mocks';
import { resetStudentDiaryReviewsMock, studentProfileHandlers } from './student-profile/mocks';
import { todayHandlers } from './today/mocks';

/**
 * Admin kundalik nazorat bo'limlari (Bugun, Arizalar, Yuz tasdiqlash, Kundaliklar, Kalendar,
 * Xarita, Baholash) va talaba profili (davomat/kundalik) MSW handler'lari — v3.28 dan oldin
 * tyutor bo'limida edi. `src/mocks/handlers.ts` ro'yxatiga `...opsHandlers` sifatida qo'shiladi.
 */
export const opsHandlers: HttpHandler[] = [
  ...todayHandlers,
  ...locationHandlers,
  ...applicationsHandlers,
  ...studentProfileHandlers,
  ...diariesHandlers,
  ...calendarHandlers,
  ...mapHandlers,
  ...gradingHandlers,
  ...faceHandlers,
];

/** Mutatsiyalar o'zgartirgan in-memory holatni tiklash (testlar orasida). */
export function resetOpsMocks() {
  resetApplicationsMock();
  resetDiariesMock();
  resetGradingMock();
  resetStudentDiaryReviewsMock();
  resetFaceMock();
  resetStudentPasswordMock();
}

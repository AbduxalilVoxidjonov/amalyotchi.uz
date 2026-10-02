import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { resetMessagesMock } from '@/features/admin/messages/mocks';
import { resetStudentsMock } from '@/features/admin/students/mocks';
import { resetCheckinQrMock } from '@/features/shared/checkin-qr/mocks';
import { resetTutorMocks } from '@/features/tutor/mocks';
import { resetMockState } from '@/mocks/data';
import { server } from '@/mocks/server';
import { useAuthStore } from '@/shared/auth/store';

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));

afterEach(() => {
  cleanup();
  server.resetHandlers();
  resetMockState();
  // Mutatsiya qiladigan mock'lar (ariza qarori, kundalik bahosi, ruxsat, baholash) — testlar bir-biriga ta'sir qilmasin.
  resetTutorMocks();
  resetCheckinQrMock();
  resetMessagesMock();
  resetStudentsMock();
  useAuthStore.getState().clear();
  window.localStorage.clear();
});

afterAll(() => server.close());

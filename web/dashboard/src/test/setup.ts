import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { resetMockState } from '@/mocks/data';
import { server } from '@/mocks/server';
import { useAuthStore } from '@/shared/auth/store';

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));

afterEach(() => {
  cleanup();
  server.resetHandlers();
  resetMockState();
  useAuthStore.getState().clear();
  window.localStorage.clear();
});

afterAll(() => server.close());

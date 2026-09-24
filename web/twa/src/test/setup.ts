import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, vi } from 'vitest';
import { resetMockState } from '@/mocks/data';
import { server } from '@/mocks/server';
import { useSessionFlags } from '@/shared/auth/session';
import { useAuthStore } from '@/shared/auth/store';
import { resetTelegramStub, setInitData, webAppStub } from './telegram-stub';

// Haqiqiy SDK telegram-web-app.js ni yuklaydi; testda stub yetarli.
vi.mock('@twa-dev/sdk', () => ({ default: webAppStub }));

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => {
  cleanup();
  server.resetHandlers();
  resetMockState();
  setInitData('');
  resetTelegramStub();
  useAuthStore.getState().clear();
  useSessionFlags.getState().reset();
  window.sessionStorage.clear();
});
afterAll(() => server.close());

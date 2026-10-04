import { QueryClient } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { RouterProvider } from 'react-router-dom';
import { afterEach } from 'vitest';
import { AppProviders } from '@/app/providers';
import { createTestRouter } from '@/app/router';
import { issueSession, mockUsers } from '@/mocks/data';
import { useAuthStore } from '@/shared/auth/store';
import { resetTutorMocks } from './mocks';

// Bu modulni import qilgan har test fayli mutatsiyalardan keyin mock holatini tiklaydi.
afterEach(() => resetTutorMocks());

/** Tyutor sifatida kirgan holda AppShell ichida berilgan marshrutni render qiladi. */
export function renderTutorRoute(path: string) {
  useAuthStore.getState().setSession(issueSession(mockUsers[1]!));
  const router = createTestRouter([path]);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>,
  );
  return router;
}

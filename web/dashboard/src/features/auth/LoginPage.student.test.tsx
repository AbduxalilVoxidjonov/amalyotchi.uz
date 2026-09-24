import { QueryClient } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RouterProvider } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AppProviders } from '@/app/providers';
import { createTestRouter } from '@/app/router';
import { mockSessions } from '@/mocks/data';
import { server } from '@/mocks/server';
import { useAuthStore } from '@/shared/auth/store';

function renderLogin() {
  const router = createTestRouter(['/login']);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>,
  );
  return router;
}

async function loginAsStudent() {
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('HEMIS ID'), '341030');
  await user.type(screen.getByLabelText('Parol'), 'student12345');
  await user.click(screen.getByRole('button', { name: 'Kirish' }));
}

afterEach(() => {
  vi.unstubAllEnvs();
  server.events.removeAllListeners();
});

describe('LoginPage · talaba roli', () => {
  it("talaba 200 olsa ham sessiya saqlanmaydi, /api/auth/logout chaqiriladi, TWA havolasi ko'rsatiladi", async () => {
    vi.stubEnv('VITE_TWA_URL', 'https://app.amaliyotchi.test');
    const logouts: string[] = [];
    server.events.on('response:mocked', ({ request, response }) => {
      if (new URL(request.url).pathname === '/api/auth/logout')
        logouts.push(`${response.status} ${request.headers.get('authorization') ?? ''}`);
    });
    const router = renderLogin();
    await loginAsStudent();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Talabalar uchun alohida ilova: https://app.amaliyotchi.test');
    expect(
      within(alert).getByRole('link', { name: 'https://app.amaliyotchi.test' }),
    ).toHaveAttribute('href', 'https://app.amaliyotchi.test');

    expect(router.state.location.pathname).toBe('/login');
    const auth = useAuthStore.getState();
    expect(auth.status).not.toBe('authenticated');
    expect(auth.accessToken).toBeNull();
    expect(auth.refreshToken).toBeNull();
    expect(window.localStorage.getItem('amaliyotchi.dashboard.refreshToken')).toBeNull();

    await waitFor(() => expect(logouts).toHaveLength(1));
    expect(logouts[0]).toMatch(/^204 Bearer /);
    expect(mockSessions.size).toBe(0);
  });

  it("VITE_TWA_URL bo'lmasa — 'Talabalar Telegram bot orqali kiradi'", async () => {
    vi.stubEnv('VITE_TWA_URL', '');
    renderLogin();
    await loginAsStudent();
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Talabalar Telegram bot orqali kiradi.',
    );
    expect(screen.queryByRole('link', { name: /http/ })).not.toBeInTheDocument();
    expect(useAuthStore.getState().accessToken).toBeNull();
  });
});

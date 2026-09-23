import { QueryClient } from '@tanstack/react-query';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RouterProvider } from 'react-router-dom';
import { issueSession, mockUsers } from '@/mocks/data';
import { AppProviders } from '@/app/providers';
import { createTestRouter } from '@/app/router';
import { useAuthStore } from '@/shared/auth/store';
import { formatClock } from './useClock';

function renderApp(path: string) {
  const router = createTestRouter([path]);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>,
  );
  return router;
}

function loginAs(index: 0 | 1) {
  useAuthStore.getState().setSession(issueSession(mockUsers[index]!));
}

describe('AppShell', () => {
  it('admin: sidebar admin nav, crumb, title override, user bloki', async () => {
    loginAs(0);
    renderApp('/admin');
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    const links = within(nav).getAllByRole('link');
    expect(links.map((l) => l.textContent)).toEqual([
      'Dashboard',
      'Fakultetlar11',
      'Tyutorlar18',
      'Korxonalar412',
      'Talabalar1284',
      'Amaliyot davrlari',
      'Hisobotlar',
      'Audit jurnali',
      'Sozlamalar',
    ]);
    expect(within(nav).getByRole('link', { name: 'Dashboard' })).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Umumiy dashboard');
    expect(screen.getByText('Admin · 2026-2027')).toBeInTheDocument();
    expect(screen.getByText('Admin Adminov')).toBeInTheDocument();
    expect(await screen.findByText('Jami amaliyotchi')).toBeInTheDocument(); // AdminDashboardPage
  });

  it('tyutor: tyutor nav; nav havolasi → shell ichidagi sahifa + title', async () => {
    loginAs(1);
    const router = renderApp('/tutor');
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    expect(within(nav).getAllByRole('link')).toHaveLength(10);
    expect(within(nav).getByRole('link', { name: /Bugun/ })).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Bugun');

    await userEvent.click(within(nav).getByRole('link', { name: /Kundaliklar/ }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/tutor/diaries'));
    // Lazy sahifa: title chunk yuklangach yangilanadi.
    await waitFor(() =>
      expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Kundalik hisobotlar'),
    );
    // Kundaliklar sahifasi (features/tutor/diaries) shell ichida ochiladi.
    expect(
      await screen.findByRole('article', { name: 'Kundalik: Aliyev Akmal' }),
    ).toBeInTheDocument();
    // Sidebar hali ham bor (shell ichida)
    expect(screen.getByRole('navigation', { name: 'Asosiy' })).toBeInTheDocument();
  });

  it('Chiqish → sessiya tozalanadi → /login', async () => {
    loginAs(1);
    const router = renderApp('/tutor');
    await userEvent.click(await screen.findByRole('button', { name: 'Chiqish' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(useAuthStore.getState().status).toBe('anonymous');
  });

  it('/dev/kit (DEV) ochiladi', async () => {
    await act(async () => {
      renderApp('/dev/kit');
    });
    expect(await screen.findByRole('heading', { level: 1, name: 'UI kit' })).toBeInTheDocument();
  });
});

describe('formatClock', () => {
  it('Toshkent vaqti, o‘zbekcha hafta kuni', () => {
    // 2026-10-12 09:47 Asia/Tashkent (UTC+5) = 04:47Z; 12.10.2026 — dushanba
    expect(formatClock(new Date('2026-10-12T04:47:00Z'))).toBe('12.10.2026 · Dushanba · 09:47');
  });
});

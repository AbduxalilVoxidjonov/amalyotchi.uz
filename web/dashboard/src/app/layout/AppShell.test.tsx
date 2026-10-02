import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, render, renderHook, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { RouterProvider } from 'react-router-dom';
import { tutorNavMock } from '@/app/nav-mocks';
import { AppProviders } from '@/app/providers';
import { createTestRouter } from '@/app/router';
import { mockCompanies } from '@/features/admin/companies/mocks';
import { mockFaculties } from '@/features/admin/faculties/mocks';
import { mockStudents } from '@/features/admin/students/mocks';
import { useApplicationDecision } from '@/features/tutor/applications/hooks';
import { mockApplications } from '@/features/tutor/applications/mocks';
import { issueSession, mockUsers } from '@/mocks/data';
import { server } from '@/mocks/server';
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

/** Topbar crumb matni (h1 dan oldingi element; crumb bo'sh bo'lsa — null). */
function crumbText(): string | null {
  const h1 = screen.getByRole('heading', { level: 1 });
  const prev = h1.previousElementSibling;
  return prev ? prev.textContent : null;
}

function loginAs(index: 0 | 1) {
  useAuthStore.getState().setSession(issueSession(mockUsers[index]!));
}

describe('AppShell', () => {
  it('admin: sidebar admin nav, crumb, title override, user bloki', async () => {
    loginAs(0);
    renderApp('/admin');
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    // Badge'lar — `GET /api/admin/nav` (mock: ro'yxat endpoint'larining jami).
    await waitFor(() =>
      expect(
        within(nav)
          .getAllByRole('link')
          .map((l) => l.textContent),
      ).toEqual([
        'Dashboard',
        `Fakultetlar${mockFaculties.length}`,
        expect.stringMatching(/^Tyutorlar\d+$/),
        `Korxonalar${mockCompanies.length}`,
        `Talabalar${mockStudents.length}`,
        'Xabarlar',
        'Amaliyot davrlari',
        'Hisobotlar',
        'Audit jurnali',
        'Sozlamalar',
      ]),
    );
    expect(within(nav).getByRole('link', { name: 'Dashboard' })).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Umumiy dashboard');
    expect(screen.getByText('Admin · 2026-2027')).toBeInTheDocument();
    expect(screen.getByText('Admin Adminov')).toBeInTheDocument();
    expect(await screen.findByText('Jami amaliyotchi')).toBeInTheDocument(); // AdminDashboardPage
  });

  it('admin: API sonlari badge’da (katta son formatlanadi, 0 — badge yo‘q); o‘quv yili null → "Admin"', async () => {
    server.use(
      http.get('/api/admin/nav', () =>
        HttpResponse.json({
          counts: { faculties: 0, tutors: 18, companies: 412, students: 1284 },
          context: { academicYear: null },
        }),
      ),
    );
    loginAs(0);
    renderApp('/admin');
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    // Ming ajratgich — thin space (U+2009), `formatCount` uslubi.
    await waitFor(() =>
      expect(within(nav).getByRole('link', { name: /Talabalar/ }).textContent).toBe(
        'Talabalar1\u2009284',
      ),
    );
    expect(within(nav).getByRole('link', { name: /Tyutorlar/ })).toHaveTextContent('Tyutorlar18');
    expect(within(nav).getByRole('link', { name: /Korxonalar/ })).toHaveTextContent(
      'Korxonalar412',
    );
    expect(within(nav).getByRole('link', { name: /Fakultetlar/ }).textContent).toBe('Fakultetlar');
    expect(crumbText()).toBe('Admin');
  });

  it('yuklanishda badge va soxta son yo‘q; crumb — faqat rol', async () => {
    server.use(
      http.get('/api/tutor/nav', async () => {
        await delay('infinite');
        return HttpResponse.json({});
      }),
    );
    loginAs(1);
    renderApp('/tutor');
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    expect(
      within(nav)
        .getAllByRole('link')
        .map((l) => l.textContent),
    ).toEqual([
      'Bugun',
      'Arizalar',
      'Talabalarim',
      'Kundaliklar',
      'Kalendar',
      'Xarita',
      'Korxonalar',
      'Baholash',
      'Hisobotlar',
    ]);
    expect(crumbText()).toBe('Tyutor');
  });

  it('nav API xatosi → badge yo‘q, crumb faqat rol', async () => {
    server.use(http.get('/api/admin/nav', () => new HttpResponse(null, { status: 500 })));
    loginAs(0);
    renderApp('/admin');
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    expect(await screen.findByText('Jami amaliyotchi')).toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: /Talabalar/ }).textContent).toBe('Talabalar');
    expect(crumbText()).toBe('Admin');
  });

  it('tyutor: badge va crumb — `GET /api/tutor/nav` (mock ro‘yxatlar bilan mos)', async () => {
    const expected = tutorNavMock();
    loginAs(1);
    renderApp('/tutor');
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    await waitFor(() =>
      expect(within(nav).getByRole('link', { name: /Bugun/ })).toHaveTextContent(
        `Bugun${expected.counts.today}`,
      ),
    );
    expect(within(nav).getByRole('link', { name: /Arizalar/ })).toHaveTextContent(
      `Arizalar${expected.counts.applications}`,
    );
    expect(within(nav).getByRole('link', { name: /Talabalarim/ })).toHaveTextContent(
      `Talabalarim${expected.counts.students}`,
    );
    expect(within(nav).getByRole('link', { name: /Kundaliklar/ })).toHaveTextContent(
      `Kundaliklar${expected.counts.diaries}`,
    );
    expect(crumbText()).toBe('Tyutor · 412-22, 413-22 · 3-kurs ishlab chiqarish amaliyoti');
  });

  it('tyutor: ariza qaroridan keyin "Arizalar" badge yangilanadi (nav invalidate)', async () => {
    const pending = mockApplications.filter((a) => a.status === 'submitted');
    loginAs(1);
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <AppProviders queryClient={queryClient}>
        <RouterProvider router={createTestRouter(['/tutor'])} />
      </AppProviders>,
    );
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    const link = () => within(nav).getByRole('link', { name: /Arizalar/ });
    await waitFor(() => expect(link()).toHaveTextContent(`Arizalar${pending.length}`));

    const { result } = renderHook(() => useApplicationDecision(), {
      wrapper: ({ children }) => (
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
      ),
    });
    await act(async () => {
      await result.current.mutateAsync({
        id: pending[0]!.id,
        body: { decision: 'reject', comment: 'Test' },
      });
    });
    await waitFor(() => expect(link()).toHaveTextContent(`Arizalar${pending.length - 1}`));
  });

  it('tyutor: tyutor nav; nav havolasi → shell ichidagi sahifa + title', async () => {
    loginAs(1);
    const router = renderApp('/tutor');
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    expect(within(nav).getAllByRole('link')).toHaveLength(9);
    expect(within(nav).queryByRole('link', { name: /Ruxsat/ })).not.toBeInTheDocument();
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

  it("tyutor: eski /tutor/leave-requests → /tutor (ruxsat so'rash moduli olib tashlangan)", async () => {
    loginAs(1);
    const router = renderApp('/tutor/leave-requests');
    await waitFor(() => expect(router.state.location.pathname).toBe('/tutor'));
    expect(await screen.findByRole('heading', { level: 1 })).toHaveTextContent('Bugun');
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

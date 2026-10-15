import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, render, renderHook, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { RouterProvider } from 'react-router-dom';
import { opsNavCounts } from '@/app/nav-mocks';
import { AppProviders } from '@/app/providers';
import { createTestRouter } from '@/app/router';
import { mockCompanies } from '@/features/admin/companies/mocks';
import { mockFaculties } from '@/features/admin/faculties/mocks';
import { mockStudents } from '@/features/admin/students/mocks';
import { useApplicationDecision } from '@/features/admin/applications/hooks';
import { mockApplications } from '@/features/admin/applications/mocks';
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

/** Sidebar'dagi barcha yopiq bo'limlarni ochadi (bo'lim tugmalari — `aria-expanded`). */
async function expandAll(nav: HTMLElement) {
  for (const b of within(nav).queryAllByRole('button', { expanded: false })) {
    await userEvent.click(b);
  }
}

function loginAs(index: 0 | 1) {
  useAuthStore.getState().setSession(issueSession(mockUsers[index]!));
}

describe('AppShell', () => {
  it('admin: sidebar admin nav, crumb, title override, user bloki', async () => {
    loginAs(1);
    renderApp('/admin');
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    const ops = opsNavCounts();
    const withBadge = (label: string, n: number) => (n > 0 ? `${label}${n}` : label);
    // Bo'limlar: faqat aktiv ("Asosiy") ochiq; qolganlari yopiq.
    expect(
      within(nav)
        .getAllByRole('button')
        .map((b) => [b.textContent?.replace(/[\d\u2009]/g, ''), b.getAttribute('aria-expanded')]),
    ).toEqual([
      ['Asosiy', 'true'],
      ['Talabalar', 'false'],
      ['Tuzilma', 'false'],
      ['Amaliyot', 'false'],
      ['Aloqa va hisobot', 'false'],
      ['Tizim', 'false'],
    ]);
    await waitFor(() =>
      expect(
        within(nav)
          .getAllByRole('link')
          .map((l) => l.textContent),
      ).toEqual(['Dashboard', withBadge('Bugun', ops.today), 'Xarita']),
    );
    await expandAll(nav);
    // Badge'lar — `GET /api/admin/nav` (mock: ro'yxat endpoint'larining jami + kundalik nazorat sonlari).
    await waitFor(() =>
      expect(
        within(nav)
          .getAllByRole('link')
          .map((l) => l.textContent),
      ).toEqual([
        'Dashboard',
        withBadge('Bugun', ops.today),
        'Xarita',
        `Talabalar${mockStudents.length}`,
        withBadge('Arizalar', ops.applications),
        withBadge('Yuz tasdiqlash', ops.pendingFaceEnrollments),
        withBadge('Kundaliklar', ops.diaries),
        'Baholash',
        `Fakultetlar${mockFaculties.length}`,
        'Amaliyot davrlari',
        `Korxonalar${mockCompanies.length}`,
        'Kalendar',
        'Xabarlar',
        'Hisobotlar',
        'Audit jurnali',
        'Sozlamalar',
      ]),
    );
    // Oddiy admin — "Adminlar" va "Tyutorlar" bandlari yo'q.
    expect(within(nav).queryByRole('link', { name: /Adminlar/ })).not.toBeInTheDocument();
    expect(within(nav).queryByRole('link', { name: /Tyutor/ })).not.toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: 'Dashboard' })).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Umumiy dashboard');
    expect(screen.getByText('Admin · 2026-2027')).toBeInTheDocument();
    expect(screen.getByText('Nodira Saidova')).toBeInTheDocument();
    expect(screen.getByText("Admin · o'quv bo'limi")).toBeInTheDocument();
    expect(await screen.findByText('Jami amaliyotchi')).toBeInTheDocument(); // AdminDashboardPage
  });

  it('bosh admin: "Adminlar" bandi (Audit jurnalidan oldin) va rol yozuvi', async () => {
    loginAs(0);
    renderApp('/admin');
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    await expandAll(nav);
    const labels = within(nav)
      .getAllByRole('link')
      .map((l) => l.textContent ?? '');
    const admins = labels.indexOf('Adminlar');
    expect(admins).toBeGreaterThan(0);
    expect(labels[admins + 1]).toBe('Audit jurnali');
    expect(within(nav).getByRole('link', { name: 'Adminlar' })).toHaveAttribute(
      'href',
      '/admin/admins',
    );
    expect(screen.getByText("Bosh admin · o'quv bo'limi")).toBeInTheDocument();
  });

  it('oddiy admin /admin/admins ga kira olmaydi → /admin', async () => {
    loginAs(1);
    const router = renderApp('/admin/admins');
    await waitFor(() => expect(router.state.location.pathname).toBe('/admin'));
  });

  it('admin: API sonlari badge’da (katta son formatlanadi, 0 — badge yo‘q); o‘quv yili null → "Admin"', async () => {
    server.use(
      http.get('/api/admin/nav', () =>
        HttpResponse.json({
          counts: {
            faculties: 0,
            companies: 412,
            students: 1284,
            today: 38,
            applications: 7,
            diaries: 0,
            pendingFaceEnrollments: 3,
          },
          context: { academicYear: null },
        }),
      ),
    );
    loginAs(0);
    renderApp('/admin');
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    // Yopiq bo'lim sarlavhasida — faqat kutilayotgan ishlar yig'indisi (arizalar 7 + yuz 3 +
    // kundaliklar 0 = 10); talabalar jami (1 284) kirmaydi.
    await waitFor(() =>
      expect(within(nav).getByRole('button', { name: /Talabalar/ }).textContent).toBe(
        'Talabalar10',
      ),
    );
    expect(within(nav).getByRole('button', { name: /Tuzilma/ }).textContent).toBe('Tuzilma');
    // Korxonalar jami (412) — ma'lumot soni, yig'indiga kirmaydi.
    expect(within(nav).getByRole('button', { name: /Amaliyot/ }).textContent).toBe('Amaliyot');
    await expandAll(nav);
    // Ochiq bo'limda yig'indi ko'rinmaydi (bandlarda ko'rinadi).
    expect(within(nav).getByRole('button', { name: /Talabalar/ }).textContent).toBe('Talabalar');
    // Ming ajratgich — thin space (U+2009), `formatCount` uslubi.
    await waitFor(() =>
      expect(within(nav).getByRole('link', { name: /Talabalar/ }).textContent).toBe(
        'Talabalar1 284',
      ),
    );
    expect(within(nav).getByRole('link', { name: /Bugun/ })).toHaveTextContent('Bugun38');
    expect(within(nav).getByRole('link', { name: /Arizalar/ })).toHaveTextContent('Arizalar7');
    expect(within(nav).getByRole('link', { name: /Yuz tasdiqlash/ })).toHaveTextContent(
      'Yuz tasdiqlash3',
    );
    expect(within(nav).getByRole('link', { name: /Kundaliklar/ }).textContent).toBe('Kundaliklar');
    expect(within(nav).getByRole('link', { name: /Korxonalar/ })).toHaveTextContent(
      'Korxonalar412',
    );
    expect(within(nav).getByRole('link', { name: /Fakultetlar/ }).textContent).toBe('Fakultetlar');
    expect(crumbText()).toBe('Admin');
  });

  it('yuklanishda badge va soxta son yo‘q; crumb — faqat "Admin"', async () => {
    server.use(
      http.get('/api/admin/nav', async () => {
        await delay('infinite');
        return HttpResponse.json({});
      }),
    );
    loginAs(1);
    renderApp('/admin/today');
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    expect(
      within(nav)
        .getAllByRole('link')
        .every((l) => !/\d/.test(l.textContent ?? '')),
    ).toBe(true);
    expect(crumbText()).toBe('Admin');
  });

  it('nav API xatosi → badge yo‘q, crumb faqat "Admin"', async () => {
    server.use(http.get('/api/admin/nav', () => new HttpResponse(null, { status: 500 })));
    loginAs(0);
    renderApp('/admin');
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    expect(await screen.findByText('Jami amaliyotchi')).toBeInTheDocument();
    await expandAll(nav);
    expect(within(nav).getByRole('link', { name: /Talabalar/ }).textContent).toBe('Talabalar');
    expect(crumbText()).toBe('Admin');
  });

  it('ariza qaroridan keyin "Arizalar" badge yangilanadi (nav invalidate)', async () => {
    const pending = mockApplications.filter((a) => a.status === 'submitted');
    loginAs(0);
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <AppProviders queryClient={queryClient}>
        <RouterProvider router={createTestRouter(['/admin/today'])} />
      </AppProviders>,
    );
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    await userEvent.click(within(nav).getByRole('button', { name: /Talabalar/ }));
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

  it('nav havolasi → shell ichidagi sahifa + title (Bugun → Kundaliklar)', async () => {
    loginAs(0);
    const router = renderApp('/admin/today');
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    expect(within(nav).getByRole('link', { name: /Bugun/ })).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Bugun');

    await userEvent.click(within(nav).getByRole('button', { name: /Talabalar/ }));
    await userEvent.click(within(nav).getByRole('link', { name: /Kundaliklar/ }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/diaries'));
    // Lazy sahifa: title chunk yuklangach yangilanadi.
    await waitFor(() =>
      expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Kundalik hisobotlar'),
    );
    // Kundaliklar sahifasi (features/admin/diaries) shell ichida ochiladi.
    expect(
      await screen.findByRole('article', { name: 'Kundalik: Aliyev Akmal' }),
    ).toBeInTheDocument();
    // Sidebar hali ham bor (shell ichida)
    expect(screen.getByRole('navigation', { name: 'Asosiy' })).toBeInTheDocument();
  });

  it("aktiv sahifa bo'limi avtomatik ochiq; qidiruv → Enter → birinchi natija", async () => {
    loginAs(0);
    const router = renderApp('/admin/students');
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    expect(within(nav).getByRole('button', { name: /Talabalar/ })).toHaveAttribute(
      'aria-expanded',
      'true',
    );
    expect(within(nav).getByRole('link', { name: /Talabalar/ })).toHaveAttribute(
      'aria-current',
      'page',
    );

    const search = screen.getByRole('searchbox', { name: 'Sahifalarni qidirish' });
    await userEvent.type(search, 'kafedra');
    expect(
      within(nav)
        .getAllByRole('link')
        .map((l) => l.textContent?.replace(/[\d\u2009]/g, '')),
    ).toEqual(['Fakultetlar']);
    await userEvent.keyboard('{Enter}');
    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/faculties'));
    expect(search).toHaveValue('');
    // Yangi aktiv bo'lim ochildi.
    await waitFor(() =>
      expect(within(nav).getByRole('button', { name: /Tuzilma/ })).toHaveAttribute(
        'aria-expanded',
        'true',
      ),
    );
  });

  it("eski /tutor yo'llari endi yo'q (404)", async () => {
    loginAs(0);
    renderApp('/tutor');
    expect(await screen.findByText('404 — Sahifa topilmadi')).toBeInTheDocument();
  });

  it('Chiqish → sessiya tozalanadi → /login', async () => {
    loginAs(0);
    const router = renderApp('/admin/today');
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

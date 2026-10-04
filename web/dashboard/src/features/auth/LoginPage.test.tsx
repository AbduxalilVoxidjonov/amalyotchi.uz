import { QueryClient } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { RouterProvider } from 'react-router-dom';
import { AppProviders } from '@/app/providers';
import { createTestRouter } from '@/app/router';
import { issueSession, mockUsers, type MockUser } from '@/mocks/data';
import { server } from '@/mocks/server';
import { useAuthStore } from '@/shared/auth/store';

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

describe('LoginPage (mock rejim)', () => {
  it("admin sifatida kirish → /admin ga yo'naltiradi", async () => {
    const user = userEvent.setup();
    const router = renderApp('/login');

    await user.type(await screen.findByLabelText('HEMIS ID'), '100000000001');
    await user.type(screen.getByLabelText('Parol'), 'admin12345');
    await user.click(screen.getByRole('button', { name: 'Kirish' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin'));
    expect(await screen.findByRole('heading', { name: 'Umumiy dashboard' })).toBeInTheDocument();
    expect(useAuthStore.getState().user?.role).toBe('Admin');
    expect(window.localStorage.getItem('amaliyotchi.dashboard.refreshToken')).toMatch(
      /^mock-refresh-/,
    );
  });

  it("oddiy admin sifatida kirish → /admin; 'Adminlar' bandi ko'rinmaydi", async () => {
    const user = userEvent.setup();
    const router = renderApp('/login');

    await user.type(await screen.findByLabelText('HEMIS ID'), '100000000002');
    await user.type(screen.getByLabelText('Parol'), 'admin23456');
    await user.click(screen.getByRole('button', { name: 'Kirish' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin'));
    expect(useAuthStore.getState().user?.isSuperAdmin).toBe(false);
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    expect(within(nav).queryByRole('link', { name: 'Adminlar' })).not.toBeInTheDocument();
  });

  it("server tyutor (eski rol) qaytarsa — sessiya saqlanmaydi, xabar ko'rsatiladi", async () => {
    const tutor: MockUser = { ...mockUsers[1]!, id: 'legacy-tutor', role: 2 };
    server.use(http.post('/api/auth/login', () => HttpResponse.json(issueSession(tutor))));
    const user = userEvent.setup();
    const router = renderApp('/login');

    await user.type(await screen.findByLabelText('HEMIS ID'), '100000000009');
    await user.type(screen.getByLabelText('Parol'), 'tutor12345');
    await user.click(screen.getByRole('button', { name: 'Kirish' }));

    expect(await screen.findByText('Dashboard faqat administratorlar uchun.')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
    expect(useAuthStore.getState().status).not.toBe('authenticated');
  });

  it("noto'g'ri parol → 403 xabari ko'rsatiladi", async () => {
    const user = userEvent.setup();
    renderApp('/login');

    await user.type(await screen.findByLabelText('HEMIS ID'), '100000000001');
    await user.type(screen.getByLabelText('Parol'), 'notogri-parol');
    await user.click(screen.getByRole('button', { name: 'Kirish' }));

    expect(await screen.findByText("HEMIS ID yoki parol noto'g'ri.")).toBeInTheDocument();
  });

  it('zod validatsiya: qisqa parol serverga yuborilmaydi', async () => {
    const user = userEvent.setup();
    renderApp('/login');

    await user.type(await screen.findByLabelText('HEMIS ID'), '100000000001');
    await user.type(screen.getByLabelText('Parol'), '123');
    await user.click(screen.getByRole('button', { name: 'Kirish' }));

    expect(
      await screen.findByText("Parol kamida 8 ta belgidan iborat bo'lishi kerak."),
    ).toBeInTheDocument();
  });

  it("zod validatsiya: harflar bo'lgan HEMIS ID serverga yuborilmaydi", async () => {
    const user = userEvent.setup();
    renderApp('/login');

    await user.type(await screen.findByLabelText('HEMIS ID'), 'abc123');
    await user.type(screen.getByLabelText('Parol'), 'admin12345');
    await user.click(screen.getByRole('button', { name: 'Kirish' }));

    expect(
      await screen.findByText("HEMIS ID faqat raqamlardan iborat bo'lishi kerak."),
    ).toBeInTheDocument();
  });

  it('kirilmagan holda /admin → /login', async () => {
    const router = renderApp('/admin');
    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
  });

  it("eski tyutor sessiyasi /admin ga kira olmaydi → /login (sikl yo'q)", async () => {
    const tutor: MockUser = { ...mockUsers[1]!, id: 'legacy-tutor', role: 2 };
    useAuthStore.getState().setSession(issueSession(tutor));
    const router = renderApp('/admin');
    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(await screen.findByRole('button', { name: 'Kirish' })).toBeInTheDocument();
  });

  it("noma'lum manzil → 404", async () => {
    renderApp('/yoq-sahifa');
    expect(await screen.findByText(/404/)).toBeInTheDocument();
  });
});

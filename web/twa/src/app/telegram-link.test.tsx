import { fireEvent, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { useSessionFlags } from '@/shared/auth/session';
import { useAuthStore } from '@/shared/auth/store';
import { renderApp } from '@/test/render-app';

const LINK_TITLE = "Hisobingizni bog'lang";

async function submitLink(hemisId: string, password: string) {
  await screen.findByRole('heading', { name: LINK_TITLE });
  fireEvent.change(screen.getByLabelText('HEMIS ID'), { target: { value: hemisId } });
  fireEvent.change(screen.getByLabelText('Parol'), { target: { value: password } });
  fireEvent.click(screen.getByRole('button', { name: "Bog'lash va kirish" }));
}

describe("Telegram ichida hisobni bog'lash (403 → /api/auth/telegram/link)", () => {
  afterEach(() => server.events.removeAllListeners());

  it("403 → bog'lash formasi → muvaffaqiyat → ilova; so'rov body'sida initData", async () => {
    const bodies: unknown[] = [];
    server.events.on('request:start', ({ request }) => {
      if (new URL(request.url).pathname === '/api/auth/telegram/link') {
        void request
          .clone()
          .json()
          .then((b) => bodies.push(b));
      }
    });
    renderApp('/kundalik', { initData: 'unlinked' });
    expect(
      await screen.findByText(
        "Birinchi marta kiryapsiz. Tyutoringiz bergan HEMIS ID va parolni kiriting — Telegram hisobingiz bog'lanadi va keyingi safar avtomatik kirasiz.",
      ),
    ).toBeInTheDocument();
    await submitLink('341030', 'talaba12345');

    expect(
      await screen.findByRole('heading', { name: 'Kundaligim', level: 1 }),
    ).toBeInTheDocument();
    expect(useAuthStore.getState().status).toBe('authenticated');
    expect(useAuthStore.getState().user?.hemisId).toBe('341030');
    expect(bodies).toEqual([{ initData: 'unlinked', hemisId: '341030', password: 'talaba12345' }]);
  });

  it("403 (noto'g'ri parol) → `detail`, forma qoladi, sessiya yo'q", async () => {
    renderApp('/', { initData: 'unlinked' });
    await submitLink('341030', 'notogri');
    expect(await screen.findByRole('alert')).toHaveTextContent("HEMIS ID yoki parol noto'g'ri.");
    expect(screen.getByRole('heading', { name: LINK_TITLE })).toBeInTheDocument();
    expect(useAuthStore.getState().status).toBe('anonymous');
  });

  it('409 → `detail` + "Tyutoringizga murojaat qiling."', async () => {
    renderApp('/', { initData: 'unlinked' });
    await submitLink('341099', 'istalgan1');
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent("Bu Telegram akkaunti boshqa hisobga bog'langan.");
    expect(alert).toHaveTextContent('Tyutoringizga murojaat qiling.');
    expect(useAuthStore.getState().status).toBe('anonymous');
  });

  it('429 → rate-limit `detail` (409 izohisiz)', async () => {
    renderApp('/', { initData: 'unlinked' });
    await submitLink('341429', 'istalgan1');
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent("Juda ko'p urinish.");
    expect(alert).not.toHaveTextContent('Tyutoringizga murojaat qiling.');
  });

  it("mustChangePassword → majburiy parol ekrani, ilovaga o'tkazilmaydi", async () => {
    renderApp('/', { initData: 'unlinked' });
    await submitLink('341031', 'vaqtincha1');
    expect(
      await screen.findByRole('heading', { name: "Yangi parol o'rnating" }),
    ).toBeInTheDocument();
    expect(useSessionFlags.getState().mustChangePassword).toBe(true);
    expect(screen.queryByRole('navigation', { name: "Bo'limlar" })).not.toBeInTheDocument();
  });

  it('xodim roli (server sessiya bersa ham) → rad, sessiya saqlanmaydi, server sessiyasi bekor', async () => {
    const logout = vi.fn();
    server.use(
      http.post('/api/auth/telegram/link', async () => {
        const { issueSession, mockTutor } = await import('@/mocks/data');
        return HttpResponse.json(issueSession(mockTutor));
      }),
      http.post('/api/auth/logout', ({ request }) => {
        logout(request.headers.get('authorization'));
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderApp('/', { initData: 'unlinked' });
    await submitLink('100000000002', 'tyutor123');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      "Bu ilova faqat talabalar uchun. Xodimlar dashboard'dan kiradi.",
    );
    const state = useAuthStore.getState();
    expect(state.status).toBe('anonymous');
    expect(state.refreshToken).toBeNull();
    await waitFor(() => expect(logout).toHaveBeenCalledWith(expect.stringMatching(/^Bearer /)));
    expect(screen.getByRole('heading', { name: LINK_TITLE })).toBeInTheDocument();
  });

  it('tarmoq xatosi (Telegram-login) → eski "Kirish imkoni yo\'q" ekrani, forma yo\'q', async () => {
    server.use(http.post('/api/auth/telegram', () => HttpResponse.error()));
    renderApp('/', { initData: 'valid' });
    expect(await screen.findByRole('heading', { name: "Kirish imkoni yo'q" })).toBeInTheDocument();
    expect(screen.getByText(/Server bilan aloqa yo'q/)).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: LINK_TITLE })).not.toBeInTheDocument();
  });

  it('500 (Telegram-login) → "Kirish imkoni yo\'q"', async () => {
    server.use(
      http.post('/api/auth/telegram', () =>
        HttpResponse.json({ status: 500, title: 'Server xatosi' }, { status: 500 }),
      ),
    );
    renderApp('/', { initData: 'valid' });
    expect(await screen.findByRole('heading', { name: "Kirish imkoni yo'q" })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: LINK_TITLE })).not.toBeInTheDocument();
  });

  it('bog\'langandan keyin chiqish → "Hisobdan chiqdingiz" → "Qayta kirish" avtomatik kiradi', async () => {
    renderApp('/profil', { initData: 'unlinked' });
    await submitLink('341030', 'talaba12345');
    await screen.findByRole('heading', { name: 'Profil', level: 1 });

    fireEvent.click(await screen.findByRole('button', { name: 'Chiqish' }));
    expect(await screen.findByRole('heading', { name: 'Hisobdan chiqdingiz' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Qayta kirish' }));
    expect(
      await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: LINK_TITLE })).not.toBeInTheDocument();
  });
});

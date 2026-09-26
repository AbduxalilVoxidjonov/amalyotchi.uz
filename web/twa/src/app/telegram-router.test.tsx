import { QueryClient } from '@tanstack/react-query';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { RouterProvider } from 'react-router-dom';
import { useAuthStore } from '@/shared/auth/store';
import { pressTelegramBack, setInitData, webAppStub } from '@/test/telegram-stub';
import { AppProviders } from './providers';
import { createAppRouter, type AppRouter } from './router';
import { bindTelegramBackButton } from './telegram-back-button';

const TG_HASH = '#tgWebAppData=query_id%3Dabc%26hash%3Dx&tgWebAppVersion=8.0&tgWebAppPlatform=android';

function renderWith(router: AppRouter) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  render(
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>,
  );
}

const heading = (name: string) => screen.findByRole('heading', { name, level: 1 });
const tab = (name: string) =>
  within(screen.getByRole('navigation', { name: "Bo'limlar" })).getByRole('link', { name });

describe('Telegram rejimi: memory router (URL hech qachon o\'zgarmaydi)', () => {
  let push: ReturnType<typeof vi.spyOn>;
  let replace: ReturnType<typeof vi.spyOn>;
  let unbind: (() => void) | undefined;

  beforeEach(() => {
    // Telegram Mini App shu manzil bilan ochiladi: `/` + `#tgWebAppData…`.
    window.history.replaceState(null, '', `/${TG_HASH}`);
    push = vi.spyOn(window.history, 'pushState');
    replace = vi.spyOn(window.history, 'replaceState');
  });

  afterEach(() => {
    unbind?.();
    unbind = undefined;
    push.mockRestore();
    replace.mockRestore();
    window.history.replaceState(null, '', '/');
  });

  function expectUrlUntouched() {
    expect(push).not.toHaveBeenCalled();
    expect(replace).not.toHaveBeenCalled();
    expect(window.location.pathname).toBe('/');
    expect(window.location.hash).toBe(TG_HASH);
  }

  function start(initData = 'valid') {
    setInitData(initData);
    const router = createAppRouter();
    unbind = bindTelegramBackButton(router);
    renderWith(router);
    return router;
  }

  it("Telegram ichida → memory router, 5 tab bo'ylab o'tish — pushState/replaceState 0 marta", async () => {
    const router = start();
    expect(await heading('Bosh ekran')).toBeInTheDocument();
    for (const [name, path] of [
      ['Kundaligim', '/kundalik'],
      ['QR orqali belgilash', '/qr'],
      ['Korxonam', '/joyim'],
      ['Profil', '/profil'],
      ['Bosh ekran', '/'],
    ] as const) {
      fireEvent.click(tab(name));
      expect(await heading(name)).toBeInTheDocument();
      expect(router.state.location.pathname).toBe(path);
    }
    // Search param o'zgarishi ham URL'ga tegmaydi.
    await act(() => router.navigate('/kundalik?tab=2'));
    await heading('Kundaligim');
    expect(router.state.location.search).toBe('?tab=2');
    expectUrlUntouched();
  });

  it('eski /kalendar → /qr redirect (URL o\'zgarmaydi)', async () => {
    const router = start();
    await heading('Bosh ekran');
    await act(() => router.navigate('/kalendar'));
    await waitFor(() => expect(router.state.location.pathname).toBe('/qr'));
    expect(await heading('QR orqali belgilash')).toBeInTheDocument();
    expectUrlUntouched();
  });

  it.each(['/portfolio', '/ruxsat'])('redirect %s → / (URL o\'zgarmaydi)', async (path) => {
    const router = start();
    await heading('Bosh ekran');
    await act(() => router.navigate(path));
    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
    expect(await heading('Bosh ekran')).toBeInTheDocument();
    expectUrlUntouched();
  });

  it("login oqimlari: bog'lash formasi → ilova; chiqish → qayta kirish — URL o'zgarmaydi", async () => {
    start('unlinked');
    await screen.findByRole('heading', { name: "Hisobingizni bog'lang" });
    fireEvent.change(screen.getByLabelText('HEMIS ID'), { target: { value: '341030' } });
    fireEvent.change(screen.getByLabelText('Parol'), { target: { value: 'talaba12345' } });
    fireEvent.click(screen.getByRole('button', { name: "Bog'lash va kirish" }));
    expect(await heading('Bosh ekran')).toBeInTheDocument();
    expect(useAuthStore.getState().status).toBe('authenticated');

    fireEvent.click(tab('Profil'));
    await heading('Profil');
    fireEvent.click(await screen.findByRole('button', { name: 'Chiqish' }));
    expect(await screen.findByRole('heading', { name: 'Hisobdan chiqdingiz' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Qayta kirish' }));
    expect(await heading('Bosh ekran')).toBeInTheDocument();
    expectUrlUntouched();
  });

  it('BackButton: bosh ekranda yashirin, boshqa sahifada ko\'rinadi; bosilganda orqaga', async () => {
    const router = start();
    await heading('Bosh ekran');
    expect(webAppStub.BackButton.isVisible).toBe(false);
    expect(webAppStub.BackButton.onClick).toHaveBeenCalledTimes(1);

    fireEvent.click(tab('Kundaligim'));
    await heading('Kundaligim');
    expect(webAppStub.BackButton.isVisible).toBe(true);
    fireEvent.click(tab('Profil'));
    await heading('Profil');
    expect(webAppStub.BackButton.isVisible).toBe(true);

    act(() => pressTelegramBack());
    expect(await heading('Kundaligim')).toBeInTheDocument();
    expect(webAppStub.BackButton.isVisible).toBe(true);

    act(() => pressTelegramBack());
    expect(await heading('Bosh ekran')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/');
    expect(webAppStub.BackButton.isVisible).toBe(false);
    expectUrlUntouched();
  });

  it("BackButton: memory tarixi bo'sh (birinchi yozuv `/` emas) → `/` ga", async () => {
    setInitData('valid');
    const router = createAppRouter();
    // Tarixsiz holatni simulyatsiya: joriy yozuvni almashtiramiz (key 'default' emas, lekin orqada hech narsa yo'q).
    await router.navigate('/qr', { replace: true });
    unbind = bindTelegramBackButton(router);
    renderWith(router);
    await heading('QR orqali belgilash');
    expect(webAppStub.BackButton.isVisible).toBe(true);
    act(() => pressTelegramBack());
    // navigate(-1) memory'da chegarada qoladi → baribir foydalanuvchi bosh ekranga qaytishi kerak.
    expect(await heading('Bosh ekran')).toBeInTheDocument();
    expectUrlUntouched();
  });

  it("eski klient (< 6.1) → BackButton ishlatilmaydi, xato yo'q", async () => {
    webAppStub.version = '6.0';
    start();
    await heading('Bosh ekran');
    fireEvent.click(tab('Kundaligim'));
    await heading('Kundaligim');
    expect(webAppStub.BackButton.show).not.toHaveBeenCalled();
    expect(webAppStub.BackButton.onClick).not.toHaveBeenCalled();
    expectUrlUntouched();
  });
});

describe('Web rejimi: browser router (o\'zgarishsiz)', () => {
  afterEach(() => window.history.replaceState(null, '', '/'));

  it('initData yo\'q → browser router: URL yo\'li o\'zgaradi (pushState)', async () => {
    setInitData('');
    window.history.replaceState(null, '', '/kundalik');
    const push = vi.spyOn(window.history, 'pushState');
    const router = createAppRouter();
    expect(router.state.location.pathname).toBe('/kundalik');
    await act(() => router.navigate('/profil'));
    expect(push).toHaveBeenCalled();
    expect(window.location.pathname).toBe('/profil');
    push.mockRestore();
    router.dispose();
  });

  it("createAppRouter('web') — browser, createAppRouter('telegram') — memory `/` dan", () => {
    window.history.replaceState(null, '', '/qr');
    const web = createAppRouter('web');
    const tg = createAppRouter('telegram');
    expect(web.state.location.pathname).toBe('/qr');
    expect(tg.state.location.pathname).toBe('/');
    web.dispose();
    tg.dispose();
  });
});

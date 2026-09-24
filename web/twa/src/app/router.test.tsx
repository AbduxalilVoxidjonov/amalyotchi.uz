import { act, screen, waitFor, within } from '@testing-library/react';
import { useAuthStore } from '@/shared/auth/store';
import { renderApp } from '@/test/render-app';

describe('TWA router', () => {
  it('initData bor → mock login → bosh ekran (shell: sarlavha + tab-bar)', async () => {
    renderApp('/', { initData: 'query_id=abc&user=%7B%22id%22%3A1%7D&hash=x' });
    expect(
      await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 }),
    ).toBeInTheDocument();
    expect(useAuthStore.getState().user?.role).toBe('Student');
    expect(screen.getByRole('navigation', { name: "Bo'limlar" })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Kundaligim' })).toBeInTheDocument();
  });

  it("Telegram tashqarisida (initData yo'q, mock emas) → web login sahifasi", async () => {
    renderApp('/kundalik', { initData: '' });
    expect(await screen.findByRole('heading', { name: 'Tizimga kirish' })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: "Bo'limlar" })).not.toBeInTheDocument();
  });

  it("403 (hisob bog'lanmagan) → bog'lash formasi", async () => {
    renderApp('/', { initData: 'unlinked' });
    expect(
      await screen.findByRole('heading', { name: "Hisobingizni bog'lang" }),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: "Bog'lash va kirish" })).toBeInTheDocument();
    expect(useAuthStore.getState().status).toBe('anonymous');
  });

  it("barcha bo'lim yo'llari ochiladi (SPEC-NAV 3.2)", async () => {
    const router = renderApp('/kundalik');
    expect(
      await screen.findByRole('heading', { name: 'Kundaligim', level: 1 }),
    ).toBeInTheDocument();
    for (const [path, title] of [
      ['/joyim', 'Korxonam'],
      ['/kalendar', 'Kalendarim'],
      ['/profil', 'Profil'],
      ['/', 'Bosh ekran'],
    ] as const) {
      await act(() => router.navigate(path));
      await waitFor(() =>
        expect(screen.getByRole('heading', { name: title, level: 1 })).toBeInTheDocument(),
      );
    }
    await act(() => router.navigate('/yoq'));
    expect(await screen.findByText('404 — Sahifa topilmadi')).toBeInTheDocument();
  });

  it.each(['/portfolio', '/ruxsat'])('eski havola %s → bosh ekranga redirect', async (path) => {
    const router = renderApp(path);
    expect(
      await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/');
  });

  it("tab-bar: aynan 5 ta to'g'ridan-to'g'ri tab, \"Yana\" va ruxsat so'rash yo'q", async () => {
    renderApp('/');
    await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 });
    const nav = screen.getByRole('navigation', { name: "Bo'limlar" });
    const tabs = within(nav).getAllByRole('link');
    // Telegram rejimi: tab'lar `href`siz `<button role="link">` (Telegram-Android `<a href>` ni ushlaydi).
    expect(
      tabs.map((t) => [t.getAttribute('aria-label'), t.tagName, t.getAttribute('href')]),
    ).toEqual([
      ['Bosh ekran', 'BUTTON', null],
      ['Kundaligim', 'BUTTON', null],
      ['Kalendarim', 'BUTTON', null],
      ['Korxonam', 'BUTTON', null],
      ['Profil', 'BUTTON', null],
    ]);
    expect(within(nav).queryByRole('button')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Yana' })).not.toBeInTheDocument();
    expect(screen.queryByText(/Ruxsat so'rash/)).not.toBeInTheDocument();
    expect(screen.getByText('Talaba · 3-kurs ishlab chiqarish amaliyoti')).toBeInTheDocument();
  });

  it('header avatari havola emas (profilga faqat tab orqali)', async () => {
    renderApp('/');
    const title = await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 });
    const header = title.closest('header');
    expect(header).not.toBeNull();
    expect(within(header!).queryByRole('link')).not.toBeInTheDocument();
    expect(header).toHaveTextContent('AA'); // Avatar bosh harflari ko'rinadi
    expect(screen.getAllByRole('link', { name: /Profil/ })).toHaveLength(1);
  });
});

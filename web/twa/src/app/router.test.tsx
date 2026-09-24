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
    const router = renderApp('/portfolio');
    expect(await screen.findByRole('heading', { name: 'Portfolio', level: 1 })).toBeInTheDocument();
    for (const [path, title] of [
      ['/kundalik', 'Kundaligim'],
      ['/joyim', 'Amaliyot joyim'],
      ['/kalendar', 'Kalendarim'],
      ['/ruxsat', "Ruxsat so'rash"],
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

  it('"Yana" tugmasi qolgan bo\'limlarni ochadi', async () => {
    renderApp('/');
    await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 });
    await act(() => {
      screen.getByRole('button', { name: 'Yana' }).click();
    });
    const dialog = screen.getByRole('dialog', { name: 'Yana' });
    expect(dialog).toHaveTextContent("Ruxsat so'rash");
    expect(dialog).toHaveTextContent('Portfolio');
    expect(within(dialog).getByRole('link', { name: 'Profil' })).toHaveAttribute('href', '/profil');
    expect(dialog).toHaveTextContent('Aliyev Akmal');
    expect(dialog).toHaveTextContent('412-22 · 3-kurs'); // UserSummaryDto v2: groupName/course
    expect(screen.getByText('Talaba · 3-kurs ishlab chiqarish amaliyoti')).toBeInTheDocument();
  });
});

import { act, screen, waitFor } from '@testing-library/react';
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

  it("Telegram tashqarisida → kirish imkoni yo'q xabari", async () => {
    renderApp('/kundalik', { initData: '' });
    expect(await screen.findByRole('heading', { name: "Kirish imkoni yo'q" })).toBeInTheDocument();
  });

  it("403 (hisob bog'lanmagan) → maxsus ekran + backend `detail`", async () => {
    renderApp('/', { initData: 'unlinked' });
    expect(
      await screen.findByRole('heading', { name: "Telegram hisobingiz bog'lanmagan" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText('Hisob topilmadi — tyutoringizdan taklif havolasini oling.'),
    ).toBeInTheDocument();
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
    expect(dialog).toHaveTextContent('Aliyev Akmal');
    expect(dialog).toHaveTextContent('412-22 · 3-kurs'); // UserSummaryDto v2: groupName/course
    expect(screen.getByText('Talaba · 3-kurs ishlab chiqarish amaliyoti')).toBeInTheDocument();
  });
});

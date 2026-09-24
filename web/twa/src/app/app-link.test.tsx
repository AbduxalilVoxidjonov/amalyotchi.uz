import { QueryClient } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { RouterProvider } from 'react-router-dom';
import { setPeriodGap } from '@/features/today/mocks';
import { renderApp } from '@/test/render-app';
import { pressTelegramBack, setInitData, webAppStub } from '@/test/telegram-stub';
import { AppProviders } from './providers';
import { createAppRouter } from './router';
import { bindTelegramBackButton } from './telegram-back-button';

/**
 * Regressiya: Telegram-Android Mini App ichida `<a href="/…">` bosilganda (preventDefault bo'lsa ham)
 * Telegram uni tashqi havola deb ushlaydi → "Oops! Failed to load". Telegram rejimida ichki
 * navigatsiya elementlarida `href` BO'LMASLIGI kerak.
 */
const internalAnchors = () => document.querySelectorAll('a[href^="/"]');
const heading = (name: string) => screen.findByRole('heading', { name, level: 1 });
const nav = () => screen.getByRole('navigation', { name: "Bo'limlar" });
const tab = (name: string) => within(nav()).getByRole('link', { name });

describe("Telegram rejimi: ichki havolalarda href yo'q", () => {
  it.each([
    ['/', 'Bosh ekran'],
    ['/kundalik', 'Kundaligim'],
    ['/kalendar', 'Kalendarim'],
    ['/joyim', 'Korxonam'],
    ['/profil', 'Profil'],
  ])('%s — a[href^="/"] 0 ta', async (path, title) => {
    renderApp(path);
    await heading(title);
    // Bosh ekran: korxona kartasi (PlaceSummary) ham yuklanib bo'lsin.
    if (path === '/') await screen.findByRole('link', { name: 'Tech Solutions MChJ' });
    expect(internalAnchors()).toHaveLength(0);
  });

  it('bosh ekran, davrlar oralig\'i (PeriodGapCard) — "Amaliyot joyini yuborish" tugma, href yo\'q', async () => {
    setPeriodGap('upcoming');
    renderApp('/');
    const link = await screen.findByRole('link', { name: 'Amaliyot joyini yuborish' });
    expect(link.tagName).toBe('BUTTON');
    expect(internalAnchors()).toHaveLength(0);
  });

  it('404 sahifa: "Bosh ekranga" tugma orqali', async () => {
    const router = renderApp('/yoq');
    const back = await screen.findByRole('link', { name: 'Bosh ekranga' });
    expect(back.tagName).toBe('BUTTON');
    expect(internalAnchors()).toHaveLength(0);
    fireEvent.click(back);
    expect(await heading('Bosh ekran')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/');
  });

  it("tab-bar: tugmalar navigatsiya qiladi, aria-current faqat faol tab'da", async () => {
    const router = renderApp('/');
    await heading('Bosh ekran');
    expect(tab('Bosh ekran')).toHaveAttribute('aria-current', 'page');
    expect(tab('Bosh ekran')).toHaveAttribute('type', 'button');

    fireEvent.click(tab('Kundaligim'));
    expect(await heading('Kundaligim')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/kundalik');
    expect(tab('Kundaligim')).toHaveAttribute('aria-current', 'page');
    // `/` — `end`: boshqa sahifada faol emas.
    expect(tab('Bosh ekran')).not.toHaveAttribute('aria-current');
    expect(
      within(nav())
        .getAllByRole('link')
        .filter((t) => t.hasAttribute('aria-current')),
    ).toHaveLength(1);
    expect(internalAnchors()).toHaveLength(0);
  });

  it('bosh ekran kartasi (PlaceSummary) → /joyim', async () => {
    const router = renderApp('/');
    fireEvent.click(await screen.findByRole('link', { name: 'Tech Solutions MChJ' }));
    expect(await heading('Korxonam')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/joyim');
  });
});

describe('Telegram BackButton: tugma-navigatsiya PUSH sifatida sanaladi', () => {
  let unbind: (() => void) | undefined;
  afterEach(() => {
    unbind?.();
    unbind = undefined;
  });

  it("tab / karta bosilgach BackButton ko'rinadi, orqaga — to'g'ri ketma-ketlikda", async () => {
    setInitData('valid');
    const router = createAppRouter();
    const actions: string[] = [];
    const unsub = router.subscribe((s) => actions.push(s.historyAction));
    unbind = bindTelegramBackButton(router);
    render(
      <AppProviders
        queryClient={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
      >
        <RouterProvider router={router} />
      </AppProviders>,
    );
    await heading('Bosh ekran');
    expect(webAppStub.BackButton.isVisible).toBe(false);

    fireEvent.click(await screen.findByRole('link', { name: 'Tech Solutions MChJ' }));
    await heading('Korxonam');
    expect(webAppStub.BackButton.isVisible).toBe(true);

    fireEvent.click(tab('Profil'));
    await heading('Profil');
    expect(actions.filter((a) => a === 'PUSH')).toHaveLength(2);

    act(() => pressTelegramBack());
    expect(await heading('Korxonam')).toBeInTheDocument();
    expect(webAppStub.BackButton.isVisible).toBe(true);
    act(() => pressTelegramBack());
    expect(await heading('Bosh ekran')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/');
    expect(webAppStub.BackButton.isVisible).toBe(false);
    unsub();
  });
});

describe('Web rejimi: oddiy <a href> saqlanadi', () => {
  afterEach(() => window.history.replaceState(null, '', '/'));

  it('tab-bar va bosh ekran havolalari — <a href>', async () => {
    renderApp('/', { initData: '' });
    await screen.findByRole('heading', { name: 'Tizimga kirish' });
    fireEvent.change(screen.getByLabelText('HEMIS ID'), { target: { value: '341030' } });
    fireEvent.change(screen.getByLabelText('Parol'), { target: { value: 'talaba12345' } });
    fireEvent.click(screen.getByRole('button', { name: 'Kirish' }));
    await heading('Bosh ekran');

    const tabs = within(nav()).getAllByRole('link');
    expect(tabs.map((t) => [t.tagName, t.getAttribute('href')])).toEqual([
      ['A', '/'],
      ['A', '/kundalik'],
      ['A', '/kalendar'],
      ['A', '/joyim'],
      ['A', '/profil'],
    ]);
    expect(tab('Bosh ekran')).toHaveAttribute('aria-current', 'page');
    expect(await screen.findByRole('link', { name: 'Tech Solutions MChJ' })).toHaveAttribute(
      'href',
      '/joyim',
    );
  });
});

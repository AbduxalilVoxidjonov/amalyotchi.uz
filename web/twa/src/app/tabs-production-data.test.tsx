import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { initTelegram } from '@/shared/auth/telegram';
import { renderApp } from '@/test/render-app';
import {
  prodDiary,
  prodPeriodDays,
  prodPlace,
  prodProfile,
  prodToday,
} from '@/test/fixtures/student-production';
import { webAppStub } from '@/test/telegram-stub';

/**
 * Regressiya: production'dagi haqiqiy javob shakli (null maydonlar, `closed` davr, turli statuslar)
 * bilan bosh ekrandan har bir tabga TabBar orqali o'tiladi — hech bir sahifa xato ekraniga tushmasin.
 */
function useProductionData() {
  server.use(
    http.get('/api/student/today', () => HttpResponse.json(prodToday)),
    http.get('/api/student/diary', () => HttpResponse.json(prodDiary)),
    http.get('/api/student/place', () => HttpResponse.json(prodPlace)),
    http.get('/api/student/profile', () => HttpResponse.json(prodProfile)),
    http.get('/api/student/period-days', () => HttpResponse.json(prodPeriodDays)),
  );
}

const TABS = [
  ['Kundaligim', /Qayta yozish kerak/],
  // Davr tugagan (production: `closed`) — QR sahifasida belgilash o'rniga davr holati.
  ['QR orqali belgilash', /Amaliyot davri tugagan/],
  ['Korxonam', /Demo Korxona MChJ/],
  ['Profil', /Demo Tyutor/],
  ['Bosh ekran', /Amaliyot davri tugagan/],
] as const;

async function walkTabs() {
  await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 });
  expect(await screen.findByText(/Amaliyot davri tugagan/)).toBeInTheDocument();
  for (const [tab, content] of TABS) {
    const nav = screen.getByRole('navigation', { name: "Bo'limlar" });
    await act(async () => {
      fireEvent.click(within(nav).getByRole('link', { name: tab }));
    });
    expect(await screen.findByRole('heading', { name: tab, level: 1 })).toBeInTheDocument();
    expect((await screen.findAllByText(content)).length).toBeGreaterThan(0);
    expect(screen.queryByText("Sahifani ochib bo'lmadi")).not.toBeInTheDocument();
    expect(screen.queryByText(/Unexpected Application Error/)).not.toBeInTheDocument();
  }
}

describe('Production javob shakli bilan tablar', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('bosh ekrandan TabBar orqali barcha tablar ochiladi', async () => {
    useProductionData();
    renderApp('/');
    await walkTabs();
  });

  it.each(['/kundalik', '/qr', '/joyim', '/profil'])(
    "to'g'ridan-to'g'ri %s ochiladi (xato ekrani yo'q)",
    async (path) => {
      useProductionData();
      renderApp(path);
      await screen.findByRole('navigation', { name: "Bo'limlar" });
      await waitFor(() =>
        expect(screen.queryByRole('status', { name: 'Yuklanmoqda…' })).not.toBeInTheDocument(),
      );
      expect(screen.queryByText("Sahifani ochib bo'lmadi")).not.toBeInTheDocument();
    },
  );

  it("eski Telegram klienti (Bot API 6.0, qo'llanmaydigan metodlar xato tashlaydi) — tablar ishlaydi", async () => {
    webAppStub.version = '6.0';
    const unsupported = (name: string) => () => {
      throw new Error(`WebAppMethodUnsupported: ${name}`);
    };
    vi.spyOn(webAppStub, 'setHeaderColor').mockImplementation(unsupported('setHeaderColor'));
    vi.spyOn(webAppStub, 'setBackgroundColor').mockImplementation(
      unsupported('setBackgroundColor'),
    );
    vi.spyOn(webAppStub, 'setBottomBarColor').mockImplementation(unsupported('setBottomBarColor'));
    vi.spyOn(webAppStub, 'disableVerticalSwipes').mockImplementation(
      unsupported('disableVerticalSwipes'),
    );
    expect(() => initTelegram()).not.toThrow();
    useProductionData();
    renderApp('/', { initData: 'query_id=1&user=%7B%22id%22%3A1%7D&hash=x' });
    await walkTabs();
  });
});

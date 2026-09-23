import { http, HttpResponse } from 'msw';
import { cleanup, fireEvent, screen } from '@testing-library/react';
import { MOCK_AUTUMN_PERIOD, MOCK_SPRING_PERIOD } from '@/features/period/mocks';
import { mockPortfolio } from '@/features/portfolio/mocks';
import { server } from '@/mocks/server';
import { renderApp } from '@/test/render-app';

describe('PortfolioPage (isPortfolio)', () => {
  it('statlar, yakuniy baho hisobi va tyutor xulosasi', async () => {
    renderApp('/portfolio');
    expect(
      await screen.findByRole('heading', { name: 'Aliyev Akmal · 412-22' }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        '3-kurs ishlab chiqarish amaliyoti · Tech Solutions MChJ · 01.10–15.11.2026',
      ),
    ).toBeInTheDocument();
    expect(screen.getByText('Qatnashgan kunlar')).toBeInTheDocument();
    expect(screen.getByText('34/36')).toBeInTheDocument();
    expect(screen.getByText('Davomat · 40%')).toBeInTheDocument();
    expect(screen.getByText('37,6')).toBeInTheDocument();
    expect(screen.getByText('Jami · 90,8 ball')).toBeInTheDocument();
    expect(screen.getByText('Baho: 5')).toBeInTheDocument();
    expect(screen.getAllByRole('progressbar')).toHaveLength(4);
    expect(screen.getByText('N. Saidova · 16.11.2026')).toBeInTheDocument();
    // PDF hali tayyor emas (pdfUrl null) → tugma o'chiq
    expect(screen.getByRole('button', { name: 'Portfolio PDF' })).toBeDisabled();
  });

  it('grade null / company null / 404 → mos holatlar', async () => {
    server.use(
      http.get('/api/student/portfolio', () =>
        HttpResponse.json({
          ...mockPortfolio,
          grade: null,
          finalized: false,
          company: null,
          conclusion: null,
        }),
      ),
    );
    renderApp('/portfolio');
    expect(await screen.findByText("Baho hali yo'q")).toBeInTheDocument();
    expect(
      screen.getByText('3-kurs ishlab chiqarish amaliyoti · 01.10–15.11.2026'),
    ).toBeInTheDocument();
    expect(screen.getByText(/Xulosa amaliyot yakunida/)).toBeInTheDocument();

    server.use(
      http.get('/api/student/portfolio', () =>
        HttpResponse.json(
          { status: 404, title: 'Topilmadi', detail: 'Faol amaliyot davri yo‘q.' },
          { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    cleanup();
    renderApp('/portfolio');
    expect(await screen.findByText("Faol amaliyot davri yo'q")).toBeInTheDocument();
  });
});

describe('PortfolioPage — davr tanlagichi (v3.5)', () => {
  afterEach(() => server.events.removeAllListeners());

  it('periods ≥ 2 → tanlagich; tanlash ?periodId= bilan qayta yuklaydi', async () => {
    const urls: URL[] = [];
    server.events.on('request:start', ({ request }) => {
      const url = new URL(request.url);
      if (url.pathname === '/api/student/portfolio') urls.push(url);
    });
    renderApp('/portfolio');
    expect(
      await screen.findByText(
        '3-kurs ishlab chiqarish amaliyoti · Tech Solutions MChJ · 01.10–15.11.2026',
      ),
    ).toBeInTheDocument();

    const autumn = screen.getByRole('button', { name: /Kuzgi amaliyot 2026/ });
    const spring = screen.getByRole('button', { name: /Bahorgi amaliyot 2027/ });
    // Sukut — backend `periodId` (kuzgi).
    expect(autumn).toHaveAttribute('aria-pressed', 'true');
    expect(spring).toHaveAttribute('aria-pressed', 'false');
    expect(urls[0]?.searchParams.has('periodId')).toBe(false);

    fireEvent.click(spring);
    expect(await screen.findByText('Bahorgi amaliyot 2027 · 01.02–15.03.2027')).toBeInTheDocument();
    expect(urls.at(-1)?.searchParams.get('periodId')).toBe(MOCK_SPRING_PERIOD.id);
    expect(spring).toHaveAttribute('aria-pressed', 'true');
    expect(
      screen.getByText('Davr hali boshlanmagan — 01.02.2027 dan boshlanadi.'),
    ).toBeInTheDocument();
    expect(screen.getByText("Baho hali yo'q")).toBeInTheDocument();

    fireEvent.click(autumn);
    expect(await screen.findByText('Baho: 5')).toBeInTheDocument();
    expect(urls.at(-1)?.searchParams.get('periodId')).toBe(MOCK_AUTUMN_PERIOD.id);
  });

  it("bitta davr → tanlagich yo'q", async () => {
    server.use(
      http.get('/api/student/portfolio', () =>
        HttpResponse.json({ ...mockPortfolio, periods: [MOCK_AUTUMN_PERIOD] }),
      ),
    );
    renderApp('/portfolio');
    expect(await screen.findByText('Baho: 5')).toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'Amaliyot davri' })).not.toBeInTheDocument();
  });
});

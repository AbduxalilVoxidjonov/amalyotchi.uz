import { http, HttpResponse } from 'msw';
import { cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { MOCK_AUTUMN_PERIOD, MOCK_SPRING_PERIOD } from '@/features/period/mocks';
import { mockPortfolio } from '@/features/portfolio/mocks';
import { server } from '@/mocks/server';
import { renderApp } from '@/test/render-app';

/** Bosh ekrandagi "Portfolio" bo'limi (h2) — bugungi karta matnlari bilan aralashmasligi uchun. */
async function portfolioSection() {
  const heading = await screen.findByRole('heading', { name: 'Portfolio', level: 2 });
  const section = heading.closest('section');
  if (!section) throw new Error('Portfolio bo‘limi topilmadi');
  return within(section);
}

describe("Bosh ekran — portfolio bo'limi (isPortfolio)", () => {
  it('bugungi karta ostida: statlar, yakuniy baho hisobi va tyutor xulosasi', async () => {
    renderApp('/');
    // Bugungi karta ham shu ekranda.
    expect(await screen.findByRole('button', { name: 'KELDIM' })).toBeInTheDocument();
    const p = await portfolioSection();
    expect(await p.findByRole('heading', { name: 'Aliyev Akmal · 412-22' })).toBeInTheDocument();
    expect(
      p.getByText('3-kurs ishlab chiqarish amaliyoti · Tech Solutions MChJ · 01.10–15.11.2026'),
    ).toBeInTheDocument();
    expect(p.getByText('Qatnashgan kunlar')).toBeInTheDocument();
    expect(p.getByText('34/36')).toBeInTheDocument();
    expect(p.getByText('Davomat · 40%')).toBeInTheDocument();
    expect(p.getByText('37,6')).toBeInTheDocument();
    expect(p.getByText('Jami · 90,8 ball')).toBeInTheDocument();
    expect(p.getByText('Baho: 5')).toBeInTheDocument();
    expect(p.getAllByRole('progressbar')).toHaveLength(4);
    expect(p.getByRole('heading', { name: 'Tyutor xulosasi' })).toBeInTheDocument();
    expect(p.getByText('N. Saidova · 16.11.2026')).toBeInTheDocument();
    // PDF hali tayyor emas (pdfUrl null) → tugma o'chiq
    expect(p.getByRole('button', { name: 'Portfolio PDF' })).toBeDisabled();
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
    renderApp('/');
    let p = await portfolioSection();
    expect(await p.findByText("Baho hali yo'q")).toBeInTheDocument();
    expect(p.getByText('3-kurs ishlab chiqarish amaliyoti · 01.10–15.11.2026')).toBeInTheDocument();
    expect(p.getByText(/Xulosa amaliyot yakunida/)).toBeInTheDocument();

    server.use(
      http.get('/api/student/portfolio', () =>
        HttpResponse.json(
          { status: 404, title: 'Topilmadi', detail: 'Faol amaliyot davri yo‘q.' },
          { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    cleanup();
    renderApp('/');
    p = await portfolioSection();
    expect(await p.findByText("Faol amaliyot davri yo'q")).toBeInTheDocument();
  });

  it('portfolio xatosi bugungi kartani to‘xtatmaydi (alohida query va holat)', async () => {
    server.use(
      http.get('/api/student/portfolio', () =>
        HttpResponse.json(
          { status: 500, title: 'Server xatosi', detail: 'Portfolio vaqtincha mavjud emas.' },
          { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    renderApp('/');
    expect(await screen.findByRole('button', { name: 'KELDIM' })).toBeInTheDocument();
    const p = await portfolioSection();
    expect(await p.findByRole('button', { name: /Qayta urinish/ })).toBeInTheDocument();
  });

  it('portfolio kechiksa ham bugungi karta darhol ko‘rinadi', async () => {
    let release!: () => void;
    const gate = new Promise<void>((r) => (release = r));
    server.use(
      http.get('/api/student/portfolio', async () => {
        await gate;
        return HttpResponse.json(mockPortfolio);
      }),
    );
    renderApp('/');
    expect(await screen.findByRole('button', { name: 'KELDIM' })).toBeInTheDocument();
    const p = await portfolioSection();
    expect(p.queryByText('Baho: 5')).not.toBeInTheDocument();
    release();
    await waitFor(() => expect(p.getByText('Baho: 5')).toBeInTheDocument());
  });
});

describe('Bosh ekran portfolio — davr tanlagichi (v3.5)', () => {
  afterEach(() => server.events.removeAllListeners());

  it('periods ≥ 2 → tanlagich; tanlash ?periodId= bilan qayta yuklaydi', async () => {
    const urls: URL[] = [];
    server.events.on('request:start', ({ request }) => {
      const url = new URL(request.url);
      if (url.pathname === '/api/student/portfolio') urls.push(url);
    });
    renderApp('/');
    const p = await portfolioSection();
    expect(
      await p.findByText(
        '3-kurs ishlab chiqarish amaliyoti · Tech Solutions MChJ · 01.10–15.11.2026',
      ),
    ).toBeInTheDocument();

    const autumn = p.getByRole('button', { name: /Kuzgi amaliyot 2026/ });
    const spring = p.getByRole('button', { name: /Bahorgi amaliyot 2027/ });
    // Sukut — backend `periodId` (kuzgi).
    expect(autumn).toHaveAttribute('aria-pressed', 'true');
    expect(spring).toHaveAttribute('aria-pressed', 'false');
    expect(urls[0]?.searchParams.has('periodId')).toBe(false);

    fireEvent.click(spring);
    expect(await p.findByText('Bahorgi amaliyot 2027 · 01.02–15.03.2027')).toBeInTheDocument();
    expect(urls.at(-1)?.searchParams.get('periodId')).toBe(MOCK_SPRING_PERIOD.id);
    expect(spring).toHaveAttribute('aria-pressed', 'true');
    expect(p.getByText('Davr hali boshlanmagan — 01.02.2027 dan boshlanadi.')).toBeInTheDocument();
    expect(p.getByText("Baho hali yo'q")).toBeInTheDocument();

    fireEvent.click(autumn);
    expect(await p.findByText('Baho: 5')).toBeInTheDocument();
    expect(urls.at(-1)?.searchParams.get('periodId')).toBe(MOCK_AUTUMN_PERIOD.id);
  });

  it("bitta davr → tanlagich yo'q", async () => {
    server.use(
      http.get('/api/student/portfolio', () =>
        HttpResponse.json({ ...mockPortfolio, periods: [MOCK_AUTUMN_PERIOD] }),
      ),
    );
    renderApp('/');
    const p = await portfolioSection();
    expect(await p.findByText('Baho: 5')).toBeInTheDocument();
    expect(p.queryByRole('group', { name: 'Amaliyot davri' })).not.toBeInTheDocument();
  });
});

import { http, HttpResponse } from 'msw';
import { cleanup, screen } from '@testing-library/react';
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

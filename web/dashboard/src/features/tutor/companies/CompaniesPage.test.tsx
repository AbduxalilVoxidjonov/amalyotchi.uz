import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { TUTOR_COMPANIES_ENDPOINT } from './api';
import { renderCompaniesRoute } from './test-utils';

describe('CompaniesPage (/tutor/companies)', () => {
  it("jadval ko'lamdagi korxonalar bilan to'ladi", async () => {
    renderCompaniesRoute();
    const table = within(await screen.findByRole('table', { name: 'Korxonalar' }));
    expect(table.getByText('Tech Solutions MChJ')).toBeInTheDocument();
    expect(table.getByText('304 512 889')).toBeInTheDocument();
    expect(table.getByText('450 m')).toBeInTheDocument();
    // Talaba ustuni: ko'lamda / jami.
    expect(table.getByText('3 / 4')).toBeInTheDocument();
    expect(table.getByText('5 / 21')).toBeInTheDocument();
    expect(table.getByText('Katta radius')).toBeInTheDocument();
    expect(table.getByText("Shubhali to'planish")).toHaveAttribute('data-status', 'bad');
    expect(
      table.getByRole('progressbar', { name: "Tech Solutions MChJ o'rtacha davomati" }),
    ).toHaveAttribute('aria-valuenow', '92');
  });

  it('STIR chegarasidan oshgan korxona ogohlantirish bilan belgilanadi', async () => {
    renderCompaniesRoute();
    const table = within(await screen.findByRole('table', { name: 'Korxonalar' }));
    expect(table.getByText('21/10')).toHaveAttribute('data-status', 'bad');
    expect(table.getByText("Talaba ko'p")).toHaveAttribute('data-status', 'bad');
  });

  it("qator bosilganda korxona detaliga o'tadi", async () => {
    const user = userEvent.setup();
    renderCompaniesRoute();
    await user.click(await screen.findByText('Tech Solutions MChJ'));
    expect(await screen.findByRole('heading', { name: 'Tech Solutions MChJ' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '← Korxonalar' })).toHaveAttribute(
      'href',
      '/tutor/companies',
    );
  });

  it("xato holatida xabar va qayta urinish ko'rsatiladi", async () => {
    server.use(
      http.get(TUTOR_COMPANIES_ENDPOINT, () =>
        HttpResponse.json(
          { status: 500, title: 'Server xatosi', detail: "Korxonalarni yuklab bo'lmadi." },
          { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    renderCompaniesRoute();
    expect(await screen.findByText("Korxonalarni yuklab bo'lmadi.")).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Qayta urinish' })).toBeInTheDocument();
  });

  it("ko'lamda korxona bo'lmasa bo'sh holat", async () => {
    server.use(http.get(TUTOR_COMPANIES_ENDPOINT, () => HttpResponse.json([])));
    renderCompaniesRoute();
    expect(await screen.findByText("Korxonalar yo'q")).toBeInTheDocument();
  });
});

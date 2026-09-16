import { screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { problemResponse } from '../shared/mockProblem';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { COMPANIES_ENDPOINT } from './api';
import { CompanyDetailPage } from './CompanyDetailPage';

function renderPage(companyId = 'c1') {
  return renderHierarchyPage(<CompanyDetailPage />, '/admin/companies/:companyId', [
    `/admin/companies/${companyId}`,
  ]);
}

function studentsTable() {
  return within(screen.getByRole('table', { name: 'Korxona talabalari' }));
}

describe('CompanyDetailPage', () => {
  it("breadcrumb, korxona kartasi, lokatsiya va davrlar kesimi ko'rsatiladi", async () => {
    renderPage();
    expect(await screen.findByRole('heading', { name: 'Tech Solutions MChJ' })).toBeInTheDocument();

    const breadcrumb = screen.getByRole('navigation', { name: "Yo'l" });
    expect(within(breadcrumb).getByRole('link', { name: 'Korxonalar' })).toHaveAttribute(
      'href',
      '/admin/companies',
    );

    const facts = within(screen.getByRole('region', { name: "Korxona ma'lumotlari" }));
    expect(facts.getAllByText('304 512 889').length).toBeGreaterThan(0);
    expect(facts.getByText('Toshkent, Amir Temur 108')).toBeInTheDocument();
    expect(facts.getByText('150 m')).toBeInTheDocument();
    expect(facts.getByText('Rustamov Jasur')).toBeInTheDocument();
    expect(facts.getByText('+998 90 123-45-67')).toBeInTheDocument();
    expect(facts.getByText('Xolmatov Aziz')).toBeInTheDocument();
    expect(facts.getByText('Faol')).toBeInTheDocument();
    // Lokatsiya: haqiqiy xarita yo'q — MapPlaceholder + koordinata matni.
    expect(facts.getAllByText('41.3111, 69.2797').length).toBeGreaterThan(0);
    expect(facts.getByRole('img', { name: 'Korxona lokatsiyasi' })).toBeInTheDocument();

    const periods = within(screen.getByRole('table', { name: 'Amaliyot davrlari' }));
    expect(periods.getByText('3-kurs kuzgi amaliyot')).toBeInTheDocument();
    expect(periods.getByText('01.09.2026 — 31.10.2026')).toBeInTheDocument();
  });

  it("STIR nazorati: chegara doirasida bo'lsa ogohlantirish yo'q", async () => {
    renderPage();
    const control = within(await screen.findByRole('region', { name: 'STIR nazorati' }));
    expect(control.getByText('Chegara doirasida')).toBeInTheDocument();
    expect(screen.queryByText('STIR chegarasi oshgan')).not.toBeInTheDocument();
  });

  it('overLimit korxonada STIR ogohlantirishi va "21/10" nisbati ko\'rinadi', async () => {
    renderPage('c4');
    expect(await screen.findByRole('heading', { name: 'Mega Servis MChJ' })).toBeInTheDocument();

    const control = within(screen.getByRole('region', { name: 'STIR nazorati' }));
    expect(control.getByText('STIR chegarasi oshgan')).toBeInTheDocument();
    expect(
      control.getByText(/Mega Servis MChJ korxonasiga 21 talaba biriktirilgan/),
    ).toBeInTheDocument();
    expect(control.getByText('Chegaradan oshgan')).toBeInTheDocument();

    const facts = within(screen.getByRole('region', { name: "Korxona ma'lumotlari" }));
    expect(facts.getByText('21/10')).toBeInTheDocument();
    expect(facts.getByText("Talaba ko'p")).toHaveAttribute('data-status', 'bad');
  });

  it("talabalar jadvali FISH, tyutor, davomat va holat bilan to'ladi", async () => {
    renderPage();
    expect(await screen.findByText('Aliyev Akmal')).toBeInTheDocument();
    const table = studentsTable();
    // Ism — talaba profiliga havola ("ichiga kirish").
    expect(table.getByRole('link', { name: 'Aliyev Akmal' })).toHaveAttribute(
      'href',
      '/admin/students/c1-s1',
    );
    expect(table.getByText('HEMIS 341100')).toBeInTheDocument();
    expect(table.getAllByText('412-22').length).toBeGreaterThan(0);
    expect(table.getAllByText('Nodira Saidova').length).toBeGreaterThan(0);
    expect(table.getAllByText('Tasdiqlangan').length).toBeGreaterThan(0);
    expect(table.getByRole('progressbar', { name: 'Aliyev Akmal davomati' })).toHaveAttribute(
      'aria-valuenow',
      '94',
    );
    expect(table.getByText('34/36 kun')).toBeInTheDocument();
    expect(table.getByText('Qizil bayroq')).toHaveAttribute('data-status', 'bad');
  });

  it("noma'lum korxona → 404 holati va orqaga qaytish", async () => {
    renderPage('yoq');
    expect(await screen.findByText('Korxona topilmadi.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Korxonalarga qaytish' })).toHaveAttribute(
      'href',
      '/admin/companies',
    );
  });

  it("talabalar jadvali xatosi alohida ko'rsatiladi", async () => {
    server.use(
      http.get(`${COMPANIES_ENDPOINT}/:id/students`, () =>
        problemResponse(500, 'Server xatosi', "Talabalar ro'yxatini yuklab bo'lmadi."),
      ),
    );
    renderPage();
    expect(await screen.findByRole('heading', { name: 'Tech Solutions MChJ' })).toBeInTheDocument();
    expect(await screen.findByText("Talabalar ro'yxatini yuklab bo'lmadi.")).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Qayta urinish' })).toBeInTheDocument();
  });

  it("talabalar bo'lmasa bo'sh holat ko'rsatiladi", async () => {
    server.use(http.get(`${COMPANIES_ENDPOINT}/:id/students`, () => HttpResponse.json([])));
    renderPage();
    expect(await screen.findByText("Talabalar yo'q")).toBeInTheDocument();
  });
});

import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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
  return within(screen.getByRole('table', { name: 'Aktiv amaliyotchilar' }));
}

describe('CompanyDetailPage', () => {
  it("breadcrumb, korxona kartasi, lokatsiya va aktiv davr ko'rsatiladi", async () => {
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

    expect(facts.getByText('Aktiv talabalar')).toBeInTheDocument();

    const periods = within(screen.getByRole('table', { name: 'Aktiv amaliyot davri' }));
    expect(screen.getByRole('heading', { name: 'Aktiv amaliyot davri' })).toBeInTheDocument();
    expect(periods.getByText('3-kurs kuzgi amaliyot')).toBeInTheDocument();
    expect(periods.getByText('01.09.2026 — 31.10.2026')).toBeInTheDocument();
    expect(periods.getByText('Aktiv talaba')).toBeInTheDocument();
  });

  it("faqat davom etayotgan davr ko'rsatiladi — o'tgan davr qatori yo'q", async () => {
    renderPage('c4');
    await screen.findByRole('heading', { name: 'Mega Servis MChJ' });
    const periods = within(screen.getByRole('table', { name: 'Aktiv amaliyot davri' }));
    expect(periods.getByText('3-kurs kuzgi amaliyot')).toBeInTheDocument();
    expect(periods.queryByText('2-kurs bahorgi amaliyot')).not.toBeInTheDocument();
  });

  it("tarixli, lekin aktivsiz korxona: aktiv davr va aktiv amaliyotchilar bo'sh holati", async () => {
    renderPage('c6');
    expect(await screen.findByRole('heading', { name: 'Sharq Savdo MChJ' })).toBeInTheDocument();
    const periods = within(screen.getByRole('table', { name: 'Aktiv amaliyot davri' }));
    expect(periods.getByText("Aktiv davr yo'q")).toBeInTheDocument();
    expect(
      periods.getByText("Hozirda bu korxonada davom etayotgan amaliyot davri yo'q."),
    ).toBeInTheDocument();
    expect(
      await screen.findByText("Hozirda bu korxonada aktiv amaliyot o'tayotgan talaba yo'q."),
    ).toBeInTheDocument();
    expect(studentsTable().queryAllByRole('link')).toHaveLength(0);
  });

  it("check-in QR kartasi admin endpoint'idan yuklanadi", async () => {
    renderPage();
    const qr = within(await screen.findByRole('region', { name: 'Check-in QR kodi' }));
    expect(
      await qr.findByRole('img', { name: 'Tech Solutions MChJ check-in QR kodi' }),
    ).toBeInTheDocument();
    expect(qr.getByText(/^AMLQR:1:[0-9a-f]{32}$/)).toBeInTheDocument();
    expect(qr.getByRole('button', { name: 'Chop etish' })).toBeInTheDocument();
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
      control.getByText(/Mega Servis MChJ korxonasida hozir 21 talaba aktiv/),
    ).toBeInTheDocument();
    expect(control.getByText('Chegaradan oshgan')).toBeInTheDocument();

    const facts = within(screen.getByRole('region', { name: "Korxona ma'lumotlari" }));
    expect(facts.getByText('21/10')).toBeInTheDocument();
    expect(facts.getByText("Talaba ko'p")).toHaveAttribute('data-status', 'bad');
  });

  it("aktiv amaliyotchilar jadvali FISH, tyutor, davr, davomat va holat bilan to'ladi", async () => {
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
    expect(table.getAllByText('3-kurs kuzgi amaliyot')).toHaveLength(4);
    expect(screen.getByRole('heading', { name: 'Aktiv amaliyotchilar' })).toBeInTheDocument();
    expect(screen.getByText("Hozir amaliyot o'tayotganlar: 4 ta talaba")).toBeInTheDocument();
    expect(table.getByRole('progressbar', { name: 'Aliyev Akmal davomati' })).toHaveAttribute(
      'aria-valuenow',
      '94',
    );
    expect(table.getByText('34/36 kun')).toBeInTheDocument();
    expect(table.getByText('Qizil bayroq')).toHaveAttribute('data-status', 'bad');
  });

  it("talabalar jadvalida qatorning nom bo'lmagan katagi bosilsa — talaba profili ochiladi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Aliyev Akmal');
    const row = studentsTable().getByRole('link', { name: 'Aliyev Akmal' }).closest('[role="row"]');
    // Guruh katagi (nom emas).
    await user.click(within(row as HTMLElement).getAllByRole('cell')[1]!);
    expect(await screen.findByTestId('location')).toHaveTextContent('/admin/students/c1-s1');
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

  it("regressiya: ariza holati ustuni va holat filtrlari/tab'lari yo'q", async () => {
    renderPage();
    await screen.findByText('Aliyev Akmal');
    const table = studentsTable();
    expect(table.queryByText('Ariza')).not.toBeInTheDocument();
    expect(table.queryByText('Tasdiqlangan')).not.toBeInTheDocument();
    for (const label of ['Kutilmoqda', 'Rad etilgan', "Ko'chirilgan", 'Yakunlangan', 'Yangi']) {
      expect(screen.queryByRole('tab', { name: label })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: label })).not.toBeInTheDocument();
    }
    expect(screen.queryByRole('tablist')).not.toBeInTheDocument();
  });

  it("aktiv amaliyotchilar bo'lmasa bo'sh holat ko'rsatiladi", async () => {
    server.use(http.get(`${COMPANIES_ENDPOINT}/:id/students`, () => HttpResponse.json([])));
    renderPage();
    expect(await screen.findByText("Aktiv amaliyotchilar yo'q")).toBeInTheDocument();
    expect(
      screen.getByText("Hozirda bu korxonada aktiv amaliyot o'tayotgan talaba yo'q."),
    ).toBeInTheDocument();
  });
});

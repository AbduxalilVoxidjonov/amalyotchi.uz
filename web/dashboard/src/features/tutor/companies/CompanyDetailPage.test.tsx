import { screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { TUTOR_COMPANIES_ENDPOINT } from './api';
import { renderCompaniesRoute } from './test-utils';

function renderDetail(companyId = 'c1') {
  return renderCompaniesRoute(`/tutor/companies/${companyId}`);
}

describe('CompanyDetailPage (/tutor/companies/:companyId)', () => {
  it("check-in QR kartasi tyutor endpoint'idan yuklanadi", async () => {
    renderDetail();
    const qr = within(await screen.findByRole('region', { name: 'Check-in QR kodi' }));
    expect(
      await qr.findByRole('img', { name: 'Tech Solutions MChJ check-in QR kodi' }),
    ).toBeInTheDocument();
    expect(qr.getByText(/^AMLQR:1:[0-9a-f]{32}$/)).toBeInTheDocument();
    expect(qr.getByRole('button', { name: 'Yuklab olish (PNG)' })).toBeInTheDocument();
    expect(qr.getByRole('button', { name: 'Yangilash' })).toBeInTheDocument();
  });

  it("korxona kartasi, lokatsiya, davrlar va talabalar ko'rsatiladi", async () => {
    renderDetail();
    expect(await screen.findByRole('heading', { name: 'Tech Solutions MChJ' })).toBeInTheDocument();

    const facts = within(screen.getByRole('region', { name: "Korxona ma'lumotlari" }));
    expect(facts.getAllByText('304 512 889').length).toBeGreaterThan(0);
    expect(facts.getByText('Toshkent, Amir Temur 108')).toBeInTheDocument();
    expect(facts.getByText('Rustamov Jasur')).toBeInTheDocument();
    expect(facts.getByText('+998 90 123 45 67')).toBeInTheDocument();
    expect(facts.getByText('3 / 4')).toBeInTheDocument();
    expect(facts.getByRole('img', { name: 'Korxona lokatsiyasi' })).toBeInTheDocument();
    expect(facts.getAllByText('41.3111, 69.2797').length).toBeGreaterThan(0);

    const periods = within(screen.getByRole('table', { name: 'Amaliyot davrlari' }));
    expect(periods.getByText('3-kurs kuzgi amaliyot')).toBeInTheDocument();
    expect(periods.getByText('01.09.2026 — 31.10.2026')).toBeInTheDocument();

    const students = within(await screen.findByRole('table', { name: 'Korxona talabalari' }));
    expect(students.getByText('Aliyev Akmal')).toBeInTheDocument();
    // Ism — talaba profiliga havola ("ichiga kirish").
    expect(students.getByRole('link', { name: 'Aliyev Akmal' })).toHaveAttribute(
      'href',
      '/tutor/students/c1-s1',
    );
    expect(students.getByText('HEMIS 341400')).toBeInTheDocument();
    expect(students.getByRole('progressbar', { name: 'Aliyev Akmal davomati' })).toHaveAttribute(
      'aria-valuenow',
      '94',
    );
    expect(students.getByText('34/36 kun')).toBeInTheDocument();
  });

  it("chegara doirasidagi korxonada STIR ogohlantirishi yo'q", async () => {
    renderDetail();
    const control = within(await screen.findByRole('region', { name: 'STIR nazorati' }));
    expect(control.getByText('Chegara doirasida')).toBeInTheDocument();
    expect(screen.queryByText('STIR chegarasi oshgan')).not.toBeInTheDocument();
  });

  it('overLimit korxonada STIR ogohlantirishi chiqadi', async () => {
    renderDetail('c4');
    expect(await screen.findByRole('heading', { name: 'Mega Servis MChJ' })).toBeInTheDocument();
    const control = within(screen.getByRole('region', { name: 'STIR nazorati' }));
    expect(control.getByText('STIR chegarasi oshgan')).toBeInTheDocument();
    expect(control.getByText(/tizim bo'yicha 21 talaba\s+biriktirilgan/)).toBeInTheDocument();
    expect(control.getByText('21/10')).toBeInTheDocument();
    expect(control.getByText('Chegaradan oshgan')).toBeInTheDocument();
  });

  it("ko'lamdan tashqari korxona → 404 holati", async () => {
    renderDetail('yoq');
    expect(await screen.findByText('Korxona topilmadi.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Korxonalarga qaytish' })).toHaveAttribute(
      'href',
      '/tutor/companies',
    );
  });

  it("talabalar so'rovi xatosi alohida ko'rsatiladi", async () => {
    server.use(
      http.get(`${TUTOR_COMPANIES_ENDPOINT}/:id/students`, () =>
        HttpResponse.json(
          { status: 500, title: 'Server xatosi', detail: "Talabalarni yuklab bo'lmadi." },
          { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    renderDetail();
    expect(await screen.findByRole('heading', { name: 'Tech Solutions MChJ' })).toBeInTheDocument();
    expect(await screen.findByText("Talabalarni yuklab bo'lmadi.")).toBeInTheDocument();
  });

  it("ko'lamda talaba bo'lmasa bo'sh holat", async () => {
    server.use(http.get(`${TUTOR_COMPANIES_ENDPOINT}/:id/students`, () => HttpResponse.json([])));
    renderDetail();
    expect(await screen.findByText("Talabalar yo'q")).toBeInTheDocument();
  });
});

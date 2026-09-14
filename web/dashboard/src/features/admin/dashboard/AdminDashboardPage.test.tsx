import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { renderWithProviders } from '../shared/renderWithProviders';
import { AdminDashboardPage } from './AdminDashboardPage';
import { ADMIN_DASHBOARD_ENDPOINT } from './api';
import { buildStatCards, pendingKind } from './derive';
import { mockAdminDashboard } from './mocks';

describe('AdminDashboardPage', () => {
  it('v2 xom raqamlardan stat, fakultet, tyutor, audit matnlarini yasaydi', async () => {
    renderWithProviders(<AdminDashboardPage />);
    expect(await screen.findByText('Jami amaliyotchi')).toBeInTheDocument();
    // thin space (U+2009) — default normalizer bo'shliqlarni yig'adi, shuning uchun normalizer o'chiriladi
    expect(screen.getByText('1 284', { normalizer: (t) => t })).toBeInTheDocument();
    expect(screen.getByText('11 fakultet · 47 guruh')).toBeInTheDocument();
    expect(screen.getByText('178 tasi hali ulanmagan')).toBeInTheDocument();
    expect(screen.getByText('7 tasi 3 kundan ortiq')).toBeInTheDocument();
    expect(screen.getByText('kecha 89%')).toBeInTheDocument();

    expect(screen.getByText('Fakultetlar kesimida davomat')).toBeInTheDocument();
    expect(screen.getByText('286 talaba')).toBeInTheDocument();

    expect(screen.getByText('Nodira Saidova')).toBeInTheDocument();
    expect(screen.getByText('AT · 412-22, 413-22')).toBeInTheDocument();
    expect(screen.getByText('IM · 5 guruh')).toBeInTheDocument();
    expect(screen.getByText('7 ariza')).toBeInTheDocument();
    expect(screen.getByText("o'rtacha 3.5 kun")).toBeInTheDocument();
    expect(screen.getByText("o'rtacha 4 soat")).toBeInTheDocument();

    // Audit: Toshkent vaqti (UTC+5), tafsilot va kim — describe.ts
    expect(screen.getByText('12.10 09:31')).toBeInTheDocument();
    expect(
      screen.getByText('Davomat 01a0a0e1 — sabab: telefon zaryadi tugagan'),
    ).toBeInTheDocument();
    expect(screen.getByText('Korxona 01a0a0e0 — RadiusM: 200 → 120')).toBeInTheDocument();
    expect(screen.getAllByText('Nodira Saidova · tyutor')).toHaveLength(2);
    expect(screen.getByRole('link', { name: 'Hammasi' })).toHaveAttribute('href', '/admin/audit');
  });

  it('server xatosi → xabar + Qayta urinish', async () => {
    server.use(
      http.get(ADMIN_DASHBOARD_ENDPOINT, () =>
        HttpResponse.json(
          { status: 500, title: 'Xatolik', detail: 'Server ishlamayapti.' },
          { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    renderWithProviders(<AdminDashboardPage />);
    expect(await screen.findByText('Server ishlamayapti.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Qayta urinish' })).toBeInTheDocument();
  });
});

describe('derive', () => {
  it("stat kartalar: ulanmagan/kechikkan yo'q bo'lsa neytral, davomat tushsa late", () => {
    const cards = buildStatCards({
      ...mockAdminDashboard.stats,
      studentsUnlinked: 0,
      applicationsOverdue: 0,
      attendanceTodayPct: 80,
      attendanceYesterdayPct: 90,
    });
    expect(cards.map((c) => c.note)).toEqual([
      '11 fakultet · 47 guruh',
      'hammasi ulangan',
      "kechikkani yo'q",
      'kecha 90%',
    ]);
    expect(cards.map((c) => c.tone)).toEqual([undefined, 'ok', undefined, 'late']);
  });

  it('tyutor kutayotgan rangi: late → bad, bor → late, 0 → ok', () => {
    const [late, some, , none] = mockAdminDashboard.tutors;
    expect(pendingKind(late!)).toBe('bad');
    expect(pendingKind(some!)).toBe('late');
    expect(pendingKind(none!)).toBe('ok');
  });
});

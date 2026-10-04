import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { renderWithProviders } from '../shared/renderWithProviders';
import { AdminDashboardPage } from './AdminDashboardPage';
import { ADMIN_DASHBOARD_ENDPOINT } from './api';
import {
  attendanceEmptyReason,
  buildStatCards,
  facultyPctText,
  NO_ONGOING_PRACTICE,
  NOT_A_WORK_DAY,
} from './derive';
import { mockAdminDashboard } from './mocks';

describe('AdminDashboardPage', () => {
  it('v2 xom raqamlardan stat, fakultet, audit matnlarini yasaydi', async () => {
    renderWithProviders(<AdminDashboardPage />);
    expect(await screen.findByText('Jami amaliyotchi')).toBeInTheDocument();
    // thin space (U+2009) — default normalizer bo'shliqlarni yig'adi, shuning uchun normalizer o'chiriladi
    expect(screen.getByText('1 284', { normalizer: (t) => t })).toBeInTheDocument();
    expect(screen.getByText('11 fakultet · 47 guruh')).toBeInTheDocument();
    expect(screen.getByText('178 tasi hali ulanmagan')).toBeInTheDocument();
    expect(screen.getByText('Telegram ulangan')).toBeInTheDocument();
    expect(screen.getByText('7 tasi 48 soatdan ortiq')).toBeInTheDocument();
    expect(screen.getByText('92%')).toBeInTheDocument();
    expect(screen.getByText('kecha 89%')).toBeInTheDocument();

    expect(screen.getByText('Fakultetlar kesimida davomat')).toBeInTheDocument();
    expect(screen.getByText('286 talaba')).toBeInTheDocument();

    // v3.28: "Tyutorlar faolligi" bloki yo'q.
    expect(screen.queryByText('Tyutorlar faolligi')).not.toBeInTheDocument();

    // Audit: Toshkent vaqti (UTC+5), tafsilot va kim — describe.ts
    expect(screen.getByText('12.10 09:31')).toBeInTheDocument();
    expect(
      screen.getByText('Davomat 01a0a0e1 — sabab: telefon zaryadi tugagan'),
    ).toBeInTheDocument();
    expect(screen.getByText('Korxona 01a0a0e0 — RadiusM: 200 → 120')).toBeInTheDocument();
    expect(screen.getAllByText('Nodira Saidova · admin')).toHaveLength(2);
    expect(screen.getByRole('link', { name: 'Hammasi' })).toHaveAttribute('href', '/admin/audit');
  });

  it("bugun davom etayotgan amaliyot yo'q → nollar o'rniga tushunarli holat, bo'sh ro'yxatlar", async () => {
    server.use(
      http.get(ADMIN_DASHBOARD_ENDPOINT, () =>
        HttpResponse.json({
          ...mockAdminDashboard,
          stats: {
            ...mockAdminDashboard.stats,
            ongoingPeriods: 0,
            expectedToday: 0,
            presentToday: 0,
            lateToday: 0,
            absentToday: 0,
            excusedToday: 0,
            noDiaryToday: 0,
            attendanceTodayPct: 0,
            expectedYesterday: 0,
            attendanceYesterdayPct: 0,
            applicationsPending: 0,
            applicationsOverdue: 0,
          },
          faculties: mockAdminDashboard.faculties.map((f) => ({
            ...f,
            expectedToday: 0,
            attendedToday: 0,
            attendancePct: 0,
          })),
          audit: [],
        }),
      ),
    );
    renderWithProviders(<AdminDashboardPage />);
    // Stat karta izohi + fakultet kartasi izohi
    expect(await screen.findAllByText(NO_ONGOING_PRACTICE)).toHaveLength(2);
    expect(screen.queryByText('0%')).not.toBeInTheDocument();
    expect(screen.getAllByText('—').length).toBeGreaterThanOrEqual(5);
    expect(screen.getByText("kechikkani yo'q")).toBeInTheDocument();
    expect(screen.getByText("Hozircha yozuv yo'q")).toBeInTheDocument();
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

  it("bugungi davomat bo'sh holati: amaliyot yo'q / ish kuni emas / kecha kutilmagan", () => {
    const s = mockAdminDashboard.stats;
    expect(attendanceEmptyReason(s)).toBeNull();
    expect(attendanceEmptyReason({ ...s, ongoingPeriods: 0, expectedToday: 0 })).toBe(
      NO_ONGOING_PRACTICE,
    );
    expect(attendanceEmptyReason({ ...s, expectedToday: 0 })).toBe(NOT_A_WORK_DAY);

    const noToday = buildStatCards({ ...s, expectedToday: 0 })[3]!;
    expect(noToday).toMatchObject({ value: '—', note: NOT_A_WORK_DAY, tone: undefined });

    const noYesterday = buildStatCards({ ...s, expectedYesterday: 0 })[3]!;
    expect(noYesterday).toMatchObject({
      value: '92%',
      note: 'kecha davomat kutilmagan',
      tone: undefined,
    });
  });

  it("fakultet foizi: bugun kutilgan talaba yo'q → '—'", () => {
    const f = mockAdminDashboard.faculties[0]!;
    expect(facultyPctText(f)).toBe('94%');
    expect(facultyPctText({ ...f, expectedToday: 0, attendancePct: 0 })).toBe('—');
  });
});
